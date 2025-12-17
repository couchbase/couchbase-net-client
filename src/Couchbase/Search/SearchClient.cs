using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Search;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Core.RateLimiting;
using Couchbase.Core.Retry.Search;
using Couchbase.KeyValue;
using Couchbase.Search.Queries.Simple;
using Couchbase.Search.Queries.Vector;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Search
{
    /// <summary>
    /// A client for making FTS <see cref="ISearchQuery"/> requests and mapping the responses to <see cref="ISearchResult"/>'s.
    /// </summary>
    /// <seealso cref="ISearchClient" />
    internal sealed class SearchClient : HttpServiceBase, ISearchClient
    {
        internal const string SearchRequiresUnreferencedMembersWarning =
            "Couchbase FTS might require types that cannot be statically analyzed. Make sure all required types are preserved.";
        internal const string SearchRequiresDynamicCodeWarning =
            "Couchbase FTS might require types that cannot be statically analyzed and might need runtime code generation. Do not use for native AOT applications.";

        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ILogger<SearchClient> _logger;
        private readonly IRequestTracer _tracer;
        private readonly IDataMapper _dataMapper;
        private readonly IAppTelemetryCollector _appTelemetryCollector;
        private string Escape(string pathValue) => Uri.EscapeDataString(pathValue);

        //for log redaction
        //private Func<object, string> User = RedactableArgument.UserAction;

        [RequiresUnreferencedCode(SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchRequiresDynamicCodeWarning)]
        public SearchClient(
            ICouchbaseHttpClientFactory httpClientFactory,
            IServiceUriProvider serviceUriProvider,
            ILogger<SearchClient> logger,
            IRequestTracer tracer,
            IAppTelemetryCollector appTelemetryCollector)
            : base(httpClientFactory)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracer = tracer;
            // Always use the SearchDataMapper
            _dataMapper = new SearchDataMapper();
            _appTelemetryCollector = appTelemetryCollector;
        }

        /// <summary>
        /// Executes a <see cref="ISearchQuery" /> request including any <see cref="SearchOptions" /> parameters asynchronously.
        /// </summary>
        /// <returns>A <see cref="ISearchResult"/> wrapped in a <see cref="Task"/> for awaiting on.</returns>
        [RequiresUnreferencedCode(SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchRequiresDynamicCodeWarning)]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2046",
            Justification = "This type may not be constructed without encountering a warning.")]
        [UnconditionalSuppressMessage("AOT", "IL3051",
            Justification = "This type may not be constructed without encountering a warning.")]
        public async Task<ISearchResult> QueryAsync(
            string indexName,
            FtsSearchRequest ftsSearchRequest,
            VectorSearch? vectorSearch,
            IScope? scope,
            CancellationToken cancellationToken)
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.SearchQuery)
                .WithLocalAddress();

            ftsSearchRequest.Query ??= new MatchNoneQuery();

            using var encodingSpan = rootSpan.EncodingSpan();

            // try get Search nodes
            var searchNode = _serviceUriProvider.GetRandomSearchNode();
            var searchUri = searchNode.SearchUri;
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? requestElapsed;

            rootSpan.WithRemoteAddress(searchUri);

            var path = scope?.Bucket?.Name is not null
                ? $"api/bucket/{Escape(scope.Bucket.Name)}/scope/{Escape(scope.Name)}/index/{Escape(indexName)}/query"
                : $"api/index/{Escape(indexName)}/query";
            var uriBuilder = new UriBuilder(searchUri)
            {
                Path = path
            };

            _logger.LogDebug("Sending FTS query with a context id {contextId} to server {searchUri}",
                ftsSearchRequest.ClientContextId, uriBuilder.ToString());

            var searchResult = new SearchResult();

            // still reliant on Newtonsoft.Json, for legacy reasons
            // if the user specified only a VectorSearch,
            // then ftsSearchRequest will have been replaced with a MatchNoneQuery
            JObject requestJson = ftsSearchRequest.ToJObject();
            if (vectorSearch is not null)
            {
                if (vectorSearch.VectorQueries.Count < 1)
                {
                    throw new InvalidArgumentException("The Vector Search query must contain at least 1 element.");
                }
                var vectorJson = JObject.FromObject(vectorSearch);
                var vectorQueries = vectorJson[VectorSearch.PropVectorQueries];
                requestJson.Add(VectorSearch.PropVectorQueries, vectorQueries);
                if (vectorSearch.VectorQueryCombination is not null)
                {
                    requestJson.Add(VectorSearch.PropVectorQueryCombination, JValue.CreateString(vectorSearch.VectorQueryCombination));
                }
            }
            //Prevents the server from returning the original request in the response.
            //Should only be sent for the new {Cluster, Scope}.SearchAsync() (not for the old {Cluster, Scope}.SearchQueryAsync())
            //but we do not differentiate between those in the SDK. The parameter is ignored by older server versions.
            requestJson.Add("showrequest", false);
            var searchBody = requestJson.ToString(Formatting.None);
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace(searchBody);
            }

            string? errors = null;
            try
            {
                using var content = new StringContent(searchBody, Encoding.UTF8, MediaType.Json);
                encodingSpan.Dispose();
                using var dispatchSpan = rootSpan.DispatchSpan(ftsSearchRequest!);
                using var httpClient = CreateHttpClient();
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, uriBuilder.Uri)
                {
                    Content = content
                };

                requestStopwatch?.Restart();

                // Search doesn't support streaming the response objects, however we can still get a small performance gain by reading
                // the HTTP response body as it arrives instead of waiting for the entire response to arrive. Therefore, use the
                // HttpClientFactory.DefaultCompletionOption here. However, the more complex logic to dispose of HttpClient used in other
                // query clients is not required as the response body will be fully read before this method returns.
                using var response = await httpClient.SendAsync(httpRequestMessage, HttpClientFactory.DefaultCompletionOption, cancellationToken)
                    .ConfigureAwait(false);
                requestElapsed = requestStopwatch?.Elapsed;
                dispatchSpan.Dispose();

                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            requestElapsed,
                            searchNode.NodesAdapter.CanonicalHostname,
                            searchNode.NodesAdapter?.AlternateHostname,
                            searchNode.NodeUuid,
                            AppTelemetryServiceType.Search,
                            AppTelemetryCounterType.Total);

                        searchResult = await _dataMapper.MapAsync<SearchResult>(stream, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        using var reader = new JsonTextReader(new StreamReader(stream));
                        var json = await JObject.LoadAsync(reader, cancellationToken).ConfigureAwait(false);
                        var queryError = json?.SelectToken("error");

                        //If the query service returned a top level error then
                        //use it otherwise the error is in the response body
                        if(queryError != null)
                        {
                            errors = queryError.Value<string>() ?? "";
                        }

                        var ctx = new SearchErrorContext
                        {
                            HttpStatus = response.StatusCode,
                            IndexName = ftsSearchRequest!.Index,
                            ClientContextId = ftsSearchRequest.ClientContextId,
                            Statement = ftsSearchRequest.Statement,
                            Errors = errors,
                            Query = ftsSearchRequest.ToJson(),
                            Message = errors
                        };

                        //Rate limiting errors
                        if (response.StatusCode == (HttpStatusCode)429 && errors != null)
                        {
                            if (errors.Contains("num_concurrent_requests"))
                            {
                                throw new RateLimitedException(RateLimitedReason.ConcurrentRequestLimitReached,
                                    ctx);
                            }
                            if (errors.Contains("num_queries_per_min"))
                            {
                                throw new RateLimitedException(RateLimitedReason.ConcurrentRequestLimitReached,
                                    ctx);
                            }
                            if (errors.Contains("ingress_mib_per_min"))
                            {
                                throw new RateLimitedException(RateLimitedReason.NetworkIngressRateLimitReached,
                                    ctx);
                            }
                            if (errors.Contains("egress_mib_per_min"))
                            {
                                throw new RateLimitedException(RateLimitedReason.NetworkEgressRateLimitReached,
                                    ctx);
                            }
                        }
                        //Quota limiting errors
                        if (response.StatusCode == HttpStatusCode.BadRequest && errors != null)
                        {
                            if (errors.Contains("index not found"))
                            {
                                throw new IndexNotFoundException("The search index was not found on the server.")
                                {
                                    Context = ctx
                                };
                            }
                            if (errors.Contains("num_fts_indexes"))
                            {
                                throw new QuotaLimitedException(QuotaLimitedReason.MaximumNumberOfIndexesReached,
                                    ctx);
                            }
                        }

                        //Internal service errors
                        if (response.StatusCode == HttpStatusCode.InternalServerError)
                        {
                            throw new InternalServerFailureException { Context = ctx };
                        }

                        //Authentication errors
                        if (response.StatusCode == HttpStatusCode.Forbidden ||
                            response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new AuthenticationFailureException(ctx);
                        }

                        throw new CouchbaseException(errors ?? "") { Context = ctx };
                    }
                }

                searchResult.HttpStatusCode = response.StatusCode;
                if (searchResult.ShouldRetry())
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        searchResult.NoRetryException = new CouchbaseException()
                        {
                            Context = new SearchErrorContext
                            {
                                HttpStatus = response.StatusCode,
                                IndexName = ftsSearchRequest!.Index,
                                ClientContextId = ftsSearchRequest.ClientContextId,
                                Statement = ftsSearchRequest.Statement,
                                Errors = errors,
                                Query = ftsSearchRequest.ToJson()
                            }
                        };
                    }
                    UpdateLastActivity();
                    return searchResult;
                }
            }
            catch (OperationCanceledException e)
            {
                requestElapsed = requestStopwatch?.Elapsed;
                //treat as an orphaned response
                rootSpan.LogOrphaned();

                _appTelemetryCollector.IncrementMetrics(
                    requestElapsed,
                    searchNode.NodesAdapter!.CanonicalHostname,
                    searchNode.NodesAdapter.AlternateHostname,
                    searchNode.NodeUuid,
                    AppTelemetryServiceType.Search,
                    AppTelemetryCounterType.TimedOut);

                _logger.LogDebug(LoggingEvents.SearchEvent, e, "Search request timeout.");
                throw new AmbiguousTimeoutException("The query was timed out via the Token.", e)
                {
                    Context = new SearchErrorContext
                    {
                        HttpStatus = HttpStatusCode.RequestTimeout,
                        IndexName = ftsSearchRequest!.Index,
                        ClientContextId = ftsSearchRequest.ClientContextId,
                        Statement = ftsSearchRequest.Statement,
                        Errors = errors,
                        Query = ftsSearchRequest.ToJson()
                    }
                };
            }
            catch (HttpRequestException e)
            {
                requestElapsed = requestStopwatch?.Elapsed;
                //treat as an orphaned response
                rootSpan.LogOrphaned();

                _appTelemetryCollector.IncrementMetrics(
                    requestElapsed,
                    searchNode.NodesAdapter!.CanonicalHostname,
                    searchNode.NodesAdapter.AlternateHostname,
                    searchNode.NodeUuid,
                    AppTelemetryServiceType.Search,
                    AppTelemetryCounterType.Canceled);

                _logger.LogDebug(LoggingEvents.SearchEvent, e, "Search request cancelled.");
                throw new RequestCanceledException("The query was canceled.", e)
                {
                    Context = new SearchErrorContext
                    {
                        HttpStatus = HttpStatusCode.RequestTimeout,
                        IndexName = ftsSearchRequest!.Index,
                        ClientContextId = ftsSearchRequest.ClientContextId,
                        Statement = ftsSearchRequest.Statement,
                        Errors = errors,
                        Query = ftsSearchRequest.ToJson()
                    }
                };
            }
            UpdateLastActivity();
            return searchResult;
        }

#region tracing
        private IRequestSpan RootSpan(string operation)
        {
            var span = _tracer.RequestSpan(operation);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.SearchQuery);
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
 *    @copyright 2015 Couchbase, Inc.
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
