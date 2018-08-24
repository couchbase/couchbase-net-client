using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Authentication;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Serialization;
using Couchbase.Tracing;
using Couchbase.Views;
using Couchbase.Utils;

namespace Couchbase.N1QL
{
    /// <summary>
    /// A <see cref="IViewClient" /> implementation for executing N1QL queries against a Couchbase Server.
    /// </summary>
    internal class QueryClient : HttpServiceBase, IQueryClient
    {
        private static readonly ILog Log = LogManager.GetLogger<QueryClient>();
        // ReSharper disable once InconsistentNaming
        internal static readonly string ERROR_5000_MSG_QUERYPORT_INDEXNOTFOUND = "queryport.indexNotFound";
        private readonly ConcurrentDictionary<string, QueryPlan> _queryCache;
        private readonly IDataMapper _queryPlanDataMapper = new JsonDataMapper(new DefaultSerializer());

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper, CouchbaseConfigContext context)
            : this(httpClient,dataMapper, new ConcurrentDictionary<string, QueryPlan>(), context)
        {
        }

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper, ConcurrentDictionary<string, QueryPlan> queryCache, ConfigContextBase context)
            : base(httpClient, dataMapper, context)
        {
            _queryCache = queryCache;
        }

        /// <summary>
        /// Executes an ad-hoc N1QL query against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="server">The <see cref="Uri"/> of the server.</param>
        /// <param name="query">A string containing a N1QL query.</param>
        /// <returns>An <see cref="IQueryResult{T}"/> implementation representing the results of the query.</returns>
        [Obsolete("Please use IQueryClient.QueryAsync(IQueryRequest, CancellationToken) instead.")]
        public Task<IQueryResult<T>> QueryAsync<T>(Uri server, string query)
        {
            var queryRequest = new QueryRequest(query).BaseUri(server);
            return QueryAsync<T>(queryRequest);
        }

        /// <summary>
        /// Executes an ad-hoc N1QL query against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="server">The <see cref="Uri"/> of the server.</param>
        /// <param name="query">A string containing a N1QL query.</param>
        /// <returns>An <see cref="IQueryResult{T}"/> implementation representing the results of the query.</returns>
        [Obsolete("Please use IQueryClient.Query(IQueryRequest) instead.")]
        public IQueryResult<T> Query<T>(Uri server, string query)
        {
            var queryRequest = new QueryRequest(query).BaseUri(server);
            return Query<T>(queryRequest);
        }

        /// <summary>
        /// Prepare an ad-hoc N1QL statement for later execution against a Couchbase Server.
        /// </summary>
        /// <param name="toPrepare">The <see cref="IQueryRequest" /> containing a N1QL statement to be prepared.</param>
        /// <returns>
        /// A <see cref="IQueryResult{T}" /> containing  the <see cref="QueryPlan" /> representing the reusable
        /// and cachable execution plan for the statement.
        /// </returns>
        /// <remarks>
        /// Most parameters in the IQueryRequest will be ignored, appart from the Statement and the BaseUri.
        /// </remarks>
        public IQueryResult<QueryPlan> Prepare(IQueryRequest toPrepare)
        {
            using (new SynchronizationContextExclusion())
            {
                return PrepareAsync(toPrepare, CancellationToken.None).Result;
            }
        }

        /// <summary>
        /// Prepare an ad-hoc N1QL statement for later execution against a Couchbase Server asynchronously
        /// </summary>
        /// <param name="toPrepare">The <see cref="IQueryRequest" /> containing a N1QL statement to be prepared.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns>
        /// A <see cref="IQueryResult{T}" /> containing  the <see cref="QueryPlan" /> representing the reusable
        /// and cachable execution plan for the statement.
        /// </returns>
        /// <remarks>
        /// Most parameters in the IQueryRequest will be ignored, appart from the Statement and the BaseUri.
        /// </remarks>
        public async Task<IQueryResult<QueryPlan>> PrepareAsync(IQueryRequest toPrepare, CancellationToken cancellationToken)
        {
            var statement = toPrepare.GetOriginalStatement();
            if (!statement.ToUpper().StartsWith("PREPARE "))
            {
                statement = string.Concat("PREPARE ", statement);
            }
            var query = new QueryRequest(statement);
            query.BaseUri(toPrepare.GetBaseUri());
            query.DataMapper = _queryPlanDataMapper;
            return await ExecuteQueryAsync<QueryPlan>(query, cancellationToken).ContinueOnAnyContext();
        }

        /// <summary>
        /// Executes the <see cref="IQueryRequest"/> against the Couchbase server.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryRequest">The query request.</param>
        /// <returns></returns>
        public IQueryResult<T> Query<T>(IQueryRequest queryRequest)
        {
            using (new SynchronizationContextExclusion())
            {
                return QueryAsync<T>(queryRequest, CancellationToken.None).Result;
            }
        }

        /// <summary>
        /// Executes the <see cref="IQueryRequest"/> against the Couchbase server asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryRequest">The query request.</param>
        /// <returns></returns>
        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest)
        {
            return QueryAsync<T>(queryRequest, CancellationToken.None);
        }

        /// <summary>
        /// Executes the <see cref="IQueryRequest"/> against the Couchbase server asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryRequest">The query request.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns></returns>
        public async Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            //shortcut for adhoc requests
            if (queryRequest.IsAdHoc)
            {
                return await ExecuteQueryAsync<T>(queryRequest, cancellationToken).ContinueOnAnyContext();
            }

            //optimize, return an error result if optimization step cannot complete
            try
            {
                await PrepareStatementIfNotAdHocAsync(queryRequest, cancellationToken).ContinueOnAnyContext();
            }
            catch (Exception e)
            {
                var errorResult = new QueryResult<T>();
                ProcessError(e, errorResult);
                return errorResult;
            }

            // execute optimized query
            var result = await ExecuteQueryAsync<T>(queryRequest, cancellationToken).ContinueOnAnyContext();

            // if the query failed, check if the query plan should be evicted
            if (!result.Success && result.IsQueryPlanStale())
            {
                var originalStatement = queryRequest.GetOriginalStatement();
                _queryCache.TryRemove(originalStatement, out QueryPlan _);
            }

            return result;
        }

        /// <summary>
        /// Prepares the statement if the <see cref="IQueryRequest"/> is not ad-hoc and caches it for reuse.asynchronously.
        /// </summary>
        /// <param name="originalRequest">The original query request.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        private async Task PrepareStatementIfNotAdHocAsync(IQueryRequest originalRequest, CancellationToken cancellationToken)
        {
            if (originalRequest.IsAdHoc) return;

            var originalStatement = originalRequest.GetOriginalStatement();
            if (_queryCache.TryGetValue(originalStatement, out var queryPlan))
            {
                originalRequest.Prepared(queryPlan, originalStatement);
            }
            else
            {
                var result = await PrepareAsync(originalRequest, cancellationToken).ContinueOnAnyContext();
                if (!result.Success)
                {
                    throw new PrepareStatementException("Unable to optimize async statement: " + result.GetErrorsAsString());
                }
                queryPlan = result.FirstOrDefault();
                if (queryPlan != null && _queryCache.TryAdd(originalStatement, queryPlan))
                {
                    originalRequest.Prepared(queryPlan, originalStatement);
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="IDataMapper"/> to use for a given <see cref="IQueryRequest"/>
        /// </summary>
        /// <param name="queryRequest">Request to get the <see cref="IDataMapper"/> for</param>
        /// <returns><see cref="IDataMapper"/> to use for the request</returns>
        internal IDataMapper GetDataMapper(IQueryRequest queryRequest)
        {
            Log.Debug("In GetDatamapper cid{0}", queryRequest.CurrentContextId);
            if (queryRequest is IQueryRequestWithDataMapper requestWithMapper)
            {
                Log.Debug("It is IQueryRequestWithDataMapper cid{0}", queryRequest.CurrentContextId);
                return requestWithMapper.DataMapper ?? DataMapper;
            }
            Log.Debug("It is not IQueryRequestWithDataMapper cid{0}", queryRequest.CurrentContextId);
            return DataMapper;
        }

        /// <summary>
        /// Executes the <see cref="IQueryRequest"/> using HTTP POST to the Couchbase Server asynchronously.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of each row returned by the query.</typeparam>
        /// <param name="queryRequest">The query request.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns></returns>
        /// <remarks>The format for the querying is JSON</remarks>
        protected virtual async Task<IQueryResult<T>> ExecuteQueryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            var queryResult = new QueryResult<T>();
            Log.Debug("Gettting Query Uri cid{0}", queryRequest.CurrentContextId);
            if (!TryGetQueryUri(out var baseUri))
            {
                Log.Error(ExceptionUtil.EmptyUriTryingSubmitN1qlQuery);
                ProcessError(new InvalidOperationException(ExceptionUtil.EmptyUriTryingSubmitN1QlQuery), queryResult);
                return queryResult;
            }

            Log.Debug("Applying creds cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
            ApplyCredentials(queryRequest);

            Log.Debug("Removing brackets cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
            if (Log.IsDebugEnabled)
            {
                //need to remove the brackets or else string.format will fail in Log.Debug
                var req = queryRequest.ToString();
                Log.Debug(req.Replace("{", "").Replace("}", ""));
            }

            Log.Debug("Buildspan cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
            string body;
            using (ClientConfiguration.Tracer.BuildSpan(queryRequest, CouchbaseOperationNames.RequestEncoding).Start())
            {
                body = queryRequest.GetFormValuesAsJson();
            }

            Log.Debug("Getting content cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
            using (var content = new StringContent(body, System.Text.Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    using (var timer = new QueryTimer(queryRequest, new CommonLogStore(Log), ClientConfiguration.EnableQueryTiming))
                    {
                        Log.Debug("Sending query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);

                        HttpResponseMessage response;
                        using (ClientConfiguration.Tracer.BuildSpan(queryRequest, CouchbaseOperationNames.DispatchToServer).Start())
                        {
                            response = await HttpClient.PostAsync(baseUri, content, cancellationToken).ContinueOnAnyContext();
                        }

                        Log.Debug("Handling response cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                        using (var span = ClientConfiguration.Tracer.BuildSpan(queryRequest, CouchbaseOperationNames.ResponseDecoding).Start())
                        using (var stream = await response.Content.ReadAsStreamAsync().ContinueOnAnyContext())
                        {
                            Log.Debug("Mapping cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                            queryResult = GetDataMapper(queryRequest).Map<QueryResultData<T>>(stream).ToQueryResult();
                            queryResult.Success = queryResult.Status == QueryStatus.Success;
                            queryResult.HttpStatusCode = response.StatusCode;
                            Log.Trace("Received query cid{0}: {1}", queryResult.ClientContextId, queryResult.ToString());
                            timer.ClusterElapsedTime = queryResult.Metrics.ElaspedTime;

                            span.SetPeerLatencyTag(queryResult.Metrics.ElaspedTime);
                        }
                    }
                    baseUri.ClearFailed();
                }
                catch (OperationCanceledException e)
                {
                    var operationContext = OperationContext.CreateQueryContext(queryRequest.CurrentContextId, Context.BucketName, baseUri?.Authority);
                    if (queryRequest is QueryRequest request)
                    {
                        operationContext.TimeoutMicroseconds = request.TimeoutValue;
                    }

                    Log.Info(operationContext.ToString());
                    ProcessError(e, queryResult);
                }
                catch (HttpRequestException e)
                {
                    Log.Info("Failed query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    baseUri.IncrementFailed();
                    ProcessError(e, queryResult);
                    Log.Error(e);
                }
                catch (AggregateException ae)
                {
                    ae.Flatten().Handle(e =>
                    {
                        Log.Info("Failed query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                        ProcessError(e, queryResult);
                        return true;
                    });
                }
                catch (Exception e)
                {
                    Log.Info("Failed query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                    Log.Info(e);
                    ProcessError(e, queryResult);
                }
            }

            UpdateLastActivity();

            return queryResult;
        }

        /// <summary>
        /// Invalidates and clears the query cache. This method can be used to explicitly clear the internal N1QL query cache. This cache will
        /// be filled with non-adhoc query statements (query plans) to speed up those subsequent executions. Triggering this method will wipe
        /// out the complete cache, which will not cause an interruption but rather all queries need to be re-prepared internally. This method
        /// is likely to be deprecated in the future once the server side query engine distributes its state throughout the cluster.
        /// </summary>
        /// <returns>
        /// An <see cref="Int32" /> representing the size of the cache before it was cleared.
        /// </returns>
        /// <exception cref="OverflowException">The dictionary already contains the maximum number of elements (<see cref="F:System.Int32.MaxValue" />).</exception>
        public int InvalidateQueryCache()
        {
            var count = _queryCache.Count;
            _queryCache.Clear();
            return count;
        }

        /// <summary>
        /// Sets the <see cref="IQueryRequest"/> state if an error occurred during the request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ex">The ex.</param>
        /// <param name="queryResult">The query result.</param>
        private static void ProcessError<T>(Exception ex, QueryResult<T> queryResult)
        {
            const string message = "Check Exception and Error fields for details.";
            queryResult.Status = QueryStatus.Fatal;
            queryResult.HttpStatusCode = HttpStatusCode.BadRequest;
            queryResult.Success = false;
            queryResult.Message = message;
            queryResult.Exception = ex;
        }

        /// <summary>
        /// Applies the credentials if they have been set by call <see cref="Cluster.Authenticate"/>.
        /// </summary>
        /// <param name="request">The request.</param>
        protected void ApplyCredentials(IQueryRequest request)
        {
            if (ClientConfiguration.HasCredentials)
            {
                var creds = ClientConfiguration.GetCredentials(AuthContext.ClusterN1Ql);
                foreach (var cred in creds)
                {
                    request.AddCredentials(cred.Key, cred.Value, false);
                }
            }
        }

        protected bool TryGetQueryUri(out FailureCountingUri baseUri)
        {
            baseUri = Context.GetQueryUri(ClientConfiguration.QueryFailedThreshold);
            if (baseUri != null && !string.IsNullOrEmpty(baseUri.AbsoluteUri))
            {
                return true;
            }
            return false;
        }
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
