using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Query
{
    /// <summary>
    /// A <see cref="QueryClient" /> implementation for executing N1QL queries against a Couchbase Server.
    /// </summary>
    internal class QueryClient : HttpServiceBase, IQueryClient
    {
        internal const string Error5000MsgQueryPortIndexNotFound = "queryport.indexNotFound";
        internal const string TransactionsBeginWork = "BEGIN WORK";

        private static readonly string DefaultClientContextId = Guid.Empty.ToString();

        private readonly ConcurrentDictionary<string, QueryPlan> _queryCache = new();
        private readonly SystemTextJsonSerializer _queryPlanSerializer =
            SystemTextJsonSerializer.Create(QuerySerializerContext.Default);
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ITypeSerializer _serializer;
        private readonly IFallbackTypeSerializerProvider _fallbackTypeSerializerProvider;
        private readonly ILogger<QueryClient> _logger;
        private readonly IRequestTracer _tracer;
        internal bool EnhancedPreparedStatementsEnabled;
        internal bool UseReplicaEnabled;

        public QueryClient(
            ICouchbaseHttpClientFactory clientFactory,
            IServiceUriProvider serviceUriProvider,
            ITypeSerializer serializer,
            IFallbackTypeSerializerProvider fallbackTypeSerializerProvider,
            ILogger<QueryClient> logger,
            IRequestTracer tracer)
            : base(clientFactory)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (serviceUriProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceUriProvider));
            }
            if (serializer is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serializer));
            }
            if (fallbackTypeSerializerProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(fallbackTypeSerializerProvider));
            }
            if (logger is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logger));
            }
            if (tracer is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(tracer));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

            _serviceUriProvider = serviceUriProvider;
            _serializer = serializer;
            _fallbackTypeSerializerProvider = fallbackTypeSerializerProvider;
            _logger = logger;
            _tracer = tracer;
        }

        /// <inheritdoc />
        public int InvalidateQueryCache()
        {
            var count = _queryCache.Count;
            _queryCache.Clear();
            return count;
        }

        /// <inheritdoc />
        public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options)
        {
            //It's possible to reuse the QueryOptions which may cause odd threading behaviour
            //So we'll clone it if it has already been used
            options = options.CloneIfUsedAlready();

            if (options.UseReplicaHasValue && !UseReplicaEnabled)
            {
                throw new FeatureNotAvailableException("The Read from Replica feature is only supported with Couchbase Server 7.6 and later");
            }

            if (string.IsNullOrEmpty(options.CurrentContextId))
            {
                options.ClientContextId(Guid.NewGuid().ToString());
            }

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.N1QLQuery, options)
                .WithOperationId(options)
                .WithStatement(statement)
                .WithLocalAddress();

            // does this query use a prepared plan?
            if (options.IsAdHoc)
            {
                // don't use prepared plan, execute query directly
                options.Statement(statement);
                return await ExecuteQuery<T>(options, options.Serializer ?? _serializer, rootSpan).ConfigureAwait(false);
            }

            // try find cached query plan
            if (_queryCache.TryGetValue(statement, out var queryPlan))
            {
                // if an upgrade has happened, don't use query plans that have an encoded plan
                if (!EnhancedPreparedStatementsEnabled || string.IsNullOrWhiteSpace(queryPlan.EncodedPlan))
                {
                    using var prepareAndExecuteSpan = _tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Internal.PrepareAndExecute, rootSpan);

                    // plan is valid, execute query with it
                    options.Prepared(queryPlan, statement);
                    return await ExecuteQuery<T>(options, options.Serializer ?? _serializer, rootSpan).ConfigureAwait(false);
                }

                // entry is stale, remove from cache
                _queryCache.TryRemove(statement, out _);
            }

            // create prepared statement
            var prepareStatement = statement;
            if (!prepareStatement.StartsWith("PREPARE ", StringComparison.InvariantCultureIgnoreCase))
            {
                prepareStatement = $"PREPARE {statement}";
            }

            // set prepared statement
            options.Statement(prepareStatement);

            // server supports combined prepare & execute
            if (EnhancedPreparedStatementsEnabled)
            {
                _logger.LogDebug("Using enhanced prepared statement behavior for request {currentContextId}", options.CurrentContextId);
                // execute combined prepare & execute query
                options.AutoExecute(true);
                var result = await ExecuteQuery<T>(options, options.Serializer ?? _serializer, rootSpan).ConfigureAwait(false);

                // add/replace query plan name in query cache
                if (result is StreamingQueryResult<T> streamingResult) // NOTE: hack to not make 'PreparedPlanName' property public
                {
                    var plan = new QueryPlan {Name = streamingResult.PreparedPlanName, Text = statement};
                    _queryCache.AddOrUpdate(statement, plan, static (_, p) => p);
                }

                return result;
            }

            _logger.LogDebug("Using legacy prepared statement behavior for request {currentContextId}", options.CurrentContextId);

            // older style, prepare then execute
            var preparedResult = await ExecuteQuery<QueryPlan>(options, _queryPlanSerializer, rootSpan).ConfigureAwait(false);
            queryPlan = await preparedResult.FirstAsync().ConfigureAwait(false);

            // add plan to cache and execute
            _queryCache.TryAdd(statement, queryPlan);
            options.Prepared(queryPlan, statement);

            // execute query using plan
            return await ExecuteQuery<T>(options, options.Serializer ?? _serializer, rootSpan).ConfigureAwait(false);
        }

        private async Task<IQueryResult<T>> ExecuteQuery<T>(QueryOptions options, ITypeSerializer serializer, IRequestSpan span)
        {
            using var cts = options.Token.FallbackToTimeout(options.TimeoutValue ?? ClusterOptions.Default.QueryTimeout);

            var currentContextId = options.CurrentContextId ?? DefaultClientContextId;

            QueryErrorContext ErrorContextFactory(QueryResultBase<T> failedQueryResult, HttpStatusCode statusCode)
            {
                // We use a local function to capture context like options and currentContextId

                return new()
                {
                    ClientContextId = options.CurrentContextId,
                    Parameters = options.GetAllParametersAsJson(serializer),
                    Statement = options.StatementValue,
                    Message = GetErrorMessage(failedQueryResult, currentContextId, statusCode),
                    Errors = failedQueryResult.Errors,
                    HttpStatus = statusCode,
                    QueryStatus = failedQueryResult.MetaData?.Status ?? QueryStatus.Fatal
                };
            }

            // try get Query node
            Uri queryUri = options.LastDispatchedNode ?? _serviceUriProvider.GetRandomQueryUri();

            span.WithRemoteAddress(queryUri);
            using var encodingSpan = span.EncodingSpan();
            using var content = options.GetRequestBody(serializer, _fallbackTypeSerializerProvider);
            encodingSpan.Dispose();

            _logger.LogDebug("Sending query {contextId} to node {endpoint}.", options.CurrentContextId, queryUri);

            QueryResultBase<T> queryResult;
            try
            {
                using var dispatchSpan = span.DispatchSpan(options);
                var httpClient = CreateHttpClient(options.TimeoutValue ?? ClusterOptions.Default.QueryTimeout);
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, queryUri)
                    {
                        Content = content
                    };

    #if NET5_0_OR_GREATER
                    //oddly only works when set on the HttpRequestMessage - this just forwards what was set from ClusterOptions.Experiments
                    request.VersionPolicy = httpClient.DefaultVersionPolicy;
                    request.Version = httpClient.DefaultRequestVersion;
    #endif

                    var response = await httpClient.SendAsync(request, HttpClientFactory.DefaultCompletionOption, cts.FallbackToToken(options.Token))
                        .ConfigureAwait(false);
                    dispatchSpan.Dispose();

                    var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                    if (serializer is IStreamingTypeDeserializer streamingDeserializer)
                    {
                        queryResult = new StreamingQueryResult<T>(stream, streamingDeserializer, ErrorContextFactory, ownedForCleanup: httpClient);
                    }
                    else
                    {
                        queryResult = new BlockQueryResult<T>(stream, serializer, ownedForCleanup: httpClient);
                    }

                    queryResult.HttpStatusCode = response.StatusCode;
                    queryResult.Success = response.StatusCode == HttpStatusCode.OK;

                    //read the header and stop when we reach the queried rows
                    await queryResult.InitializeAsync(cts?.Token ?? options.Token).ConfigureAwait(false);

                    if (response.StatusCode != HttpStatusCode.OK || queryResult.MetaData?.Status != QueryStatus.Success)
                    {
                        _logger.LogDebug("Request {currentContextId} has failed because {status}.",
                            currentContextId, queryResult.MetaData?.Status);

                        if (queryResult.ShouldRetry(EnhancedPreparedStatementsEnabled))
                        {
                            queryResult.NoRetryException = queryResult.CreateExceptionForError(ErrorContextFactory(queryResult, response.StatusCode));
                            if (queryResult.Errors.Any(x=>x.Code == 4040 && EnhancedPreparedStatementsEnabled))
                            {
                                //clear the cache of stale query plan
                                var statement = options.StatementValue ?? string.Empty;
                                if (_queryCache.TryRemove(statement, out var queryPlan))
                                {
                                    _logger.LogDebug("Query plan is stale for {currentContextId}. Purging plan {queryPlanName}.", currentContextId, queryPlan.Name);
                                }
                            }
                            _logger.LogDebug("Request {currentContextId} is being retried.", currentContextId);
                            return queryResult;
                        }

                        var context = ErrorContextFactory(queryResult, response.StatusCode);

                        if (queryResult.MetaData?.Status == QueryStatus.Timeout)
                        {
                            if (options.IsReadOnly)
                            {
                                throw new AmbiguousTimeoutException
                                {
                                    Context = context
                                };
                            }

                            throw new UnambiguousTimeoutException
                            {
                                Context = context
                            };
                        }
                        queryResult.ThrowExceptionOnError(context);
                    }
                    else if (options.StatementValue == TransactionsBeginWork)
                    {
                        // Internal support for Transactions query node affinity
                        // If the result has a "txid" row, grab the value and add it to the affinity map.
                        queryResult.MetaData.LastDispatchedToNode = queryUri;
                    }
                }
                catch
                {
                    // Ensure the HttpClient is disposed on an exception. On success scenarios it is disposed when the caller
                    // disposes of the returned IQueryResult. HttpClient is not simply disposed in every case because doing so
                    // causes exceptions in .NET 4 when using HttpCompletionOption.ResponseHeadersRead because it closes the socket
                    // before the body is fully read.
                    httpClient.Dispose();
                    throw;
                }
            }
            catch (OperationCanceledException e)
            {
                //treat as an orphaned response
                span.LogOrphaned();

                var context = new QueryErrorContext
                {
                    ClientContextId = options.CurrentContextId,
                    Parameters = options.GetAllParametersAsJson(serializer),
                    Statement = options.StatementValue,
                    HttpStatus = HttpStatusCode.RequestTimeout,
                    QueryStatus = QueryStatus.Fatal
                };

                _logger.LogDebug(LoggingEvents.QueryEvent, e, "Request timeout.");
                if (options.IsReadOnly)
                {
                    throw new UnambiguousTimeoutException("The query was timed out via the Token.", e)
                    {
                        Context = context
                    };
                }
                throw new AmbiguousTimeoutException("The query was timed out via the Token.", e)
                {
                    Context = context
                };
            }
            catch (HttpRequestException e)
            {
                _logger.LogDebug(LoggingEvents.QueryEvent, e, "Request canceled");

                //treat as an orphaned response
                span.LogOrphaned();

                var context = new QueryErrorContext
                {
                    ClientContextId = options.CurrentContextId,
                    Parameters = options.GetAllParametersAsJson(serializer),
                    Statement = options.StatementValue,
                    HttpStatus = HttpStatusCode.RequestTimeout,
                    QueryStatus = QueryStatus.Fatal
                };

                throw new RequestCanceledException("The query was canceled.", e)
                {
                    Context = context
                };
            }

            _logger.LogDebug($"Request {options.CurrentContextId} has succeeded.");
            return queryResult;
        }

        public void UpdateClusterCapabilities(ClusterCapabilities clusterCapabilities)
        {
            if (!EnhancedPreparedStatementsEnabled && clusterCapabilities.EnhancedPreparedStatementsEnabled)
            {
                EnhancedPreparedStatementsEnabled = true;
                _logger.LogInformation("Enabling Enhanced Prepared Statements");
            }

            if (!UseReplicaEnabled && clusterCapabilities.UseReplicaEnabled)
            {
                UseReplicaEnabled = true;
                _logger.LogInformation("Enabling Read From Replica Feature");
            }

        }

        private string GetErrorMessage<T>(QueryResultBase<T> queryResult, string requestId, HttpStatusCode statusCodeFallback)
        {
            var error = queryResult?.Errors?.FirstOrDefault();
            if (error != null)
            {
                _logger.LogDebug("The request {requestId} failed because: {message} [{code}]", requestId, error.Message, error.Code);
                return $"{error.Message} [{error.Code}]";
            }

            _logger.LogDebug($"The request {{requestId}} failed for an unknown reason with HTTP {(int)statusCodeFallback}", requestId, statusCodeFallback);
            return $"HTTP {(int)statusCodeFallback}";
        }

        #region tracing
        private IRequestSpan RootSpan(string operation, QueryOptions options)
        {
            var span = _tracer.RequestSpan(operation, options.RequestSpanValue);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.N1QLQuery);
                span.SetAttribute(OuterRequestSpans.Attributes.BucketName, options.BucketName!);
                span.SetAttribute(OuterRequestSpans.Attributes.ScopeName, options.ScopeName!);
                span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            }

            return span;
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
