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

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper,  ClientConfiguration clientConfig)
            : this(httpClient,dataMapper, clientConfig, new ConcurrentDictionary<string, QueryPlan>())
        {
        }

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper, ClientConfiguration clientConfig, ConcurrentDictionary<string, QueryPlan> queryCache)
            : base(httpClient, dataMapper, clientConfig)
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
            var statement = toPrepare.GetOriginalStatement();
            if (!statement.ToUpper().StartsWith("PREPARE "))
            {
                statement = string.Concat("PREPARE ", statement);
            }
            var query = new QueryRequest(statement);
            query.BaseUri(toPrepare.GetBaseUri());
            query.DataMapper = _queryPlanDataMapper;
            return ExecuteQuery<QueryPlan>(query);
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
            //shortcut for adhoc requests
            if (queryRequest.IsAdHoc)
            {
                return ExecuteQuery<T>(queryRequest);
            }

            //optimize, return an error result if optimization step cannot complete
            try
            {
                PrepareStatementIfNotAdHoc(queryRequest);
            }
            catch (Exception e)
            {
                var errorResult = new QueryResult<T>();
                ProcessError(e, errorResult);
                return errorResult;
            }

            //execute and retry if needed
            var result = ExecuteQuery<T>(queryRequest);
            if (CheckRetry(queryRequest, result))
            {
                return Retry<T>(queryRequest);
            }
            else
            {
                return result;
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

            //execute first attempt
            var result = await ExecuteQueryAsync<T>(queryRequest, cancellationToken).ContinueOnAnyContext();
            //if needed, do a second attempt after having cleared the cache
            if (CheckRetry(queryRequest, result))
            {
                return await RetryAsync<T>(queryRequest, cancellationToken).ContinueOnAnyContext();
            }
            else
            {
                return result;
            }
        }

        /// <summary>
        /// Checks the request and result to see if a retry is waranted. Will only retry if
        /// the request is not adhoc, has not already been retried and contains a N1QL error
        /// that matches criteria for retry (errors 4050, 4070 and some 5000).
        /// </summary>
        internal static bool CheckRetry<T>(IQueryRequest request, IQueryResult<T> result)
        {
            if (result.Success || request.IsAdHoc || request.HasBeenRetried)
            {
                return false;
            }

            return result.Errors.Any(error =>
                    error.Code == (int) ErrorPrepared.Unrecognized ||
                    error.Code == (int) ErrorPrepared.UnableToDecode ||
                    error.Code == (int) ErrorPrepared.IndexNotFound ||
                    (error.Code == (int) ErrorPrepared.Generic && error.Message != null && error.Message.Contains(ERROR_5000_MSG_QUERYPORT_INDEXNOTFOUND))
            );
        }

        private IQueryResult<T> Retry<T>(IQueryRequest queryRequest)
        {
            //mark as retried, remove from cache
            string key = queryRequest.GetOriginalStatement();
            queryRequest.HasBeenRetried = true;
            QueryPlan dismissed;
            _queryCache.TryRemove(key, out dismissed);

            //re-optimize
            PrepareStatementIfNotAdHoc(queryRequest);

            //re-execute
            return ExecuteQuery<T>(queryRequest);
        }

        private async Task<IQueryResult<T>> RetryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            //mark as retried, remove from cache
            queryRequest.HasBeenRetried = true;
            QueryPlan dismissed;
            _queryCache.TryRemove(queryRequest.GetOriginalStatement(), out dismissed);

            //re-optimize asynchronously
            await PrepareStatementIfNotAdHocAsync(queryRequest, cancellationToken).ContinueOnAnyContext();

            //re-execute asynchronously
            return await ExecuteQueryAsync<T>(queryRequest, cancellationToken).ContinueOnAnyContext();
        }

        /// <summary>
        /// Prepares the statement if the <see cref="IQueryRequest"/> is not ad-hoc and caches it for reuse.
        /// </summary>
        /// <param name="originalRequest">The original query request.</param>
        private void PrepareStatementIfNotAdHoc(IQueryRequest originalRequest)
        {
            if (originalRequest.IsAdHoc) return;

            var originalStatement = originalRequest.GetOriginalStatement();
            QueryPlan queryPlan;
            if (_queryCache.TryGetValue(originalStatement, out queryPlan))
            {
                originalRequest.Prepared(queryPlan, originalStatement);
            }
            else
            {
                var result = Prepare(originalRequest);
                if (!result.Success)
                {
                    Log.Warn("Failure to prepare plan for query {0} (it will be reattempted next time it is issued): {1}",
                        originalStatement, result.GetErrorsAsString());
                    throw new PrepareStatementException("Unable to optimize statement: " + result.GetErrorsAsString());
                }
                queryPlan = result.FirstOrDefault();
                if (queryPlan != null && _queryCache.TryAdd(originalStatement, queryPlan))
                {
                    originalRequest.Prepared(queryPlan, originalStatement);
                }
            }
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
            QueryPlan queryPlan;
            if (_queryCache.TryGetValue(originalStatement, out queryPlan))
            {
                originalRequest.Prepared(queryPlan, originalStatement);
            }
            else
            {
                var result = await PrepareAsync(originalRequest, cancellationToken).ContinueOnAnyContext();
                if (!result.Success)
                {
                    Log.Warn("Failure to prepare async plan for query {0} (it will be reattempted next time it is issued): {1}",
                        originalStatement, result.GetErrorsAsString());
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
            var requestWithMapper = queryRequest as IQueryRequestWithDataMapper;

            if (requestWithMapper != null)
            {
                return requestWithMapper.DataMapper ?? DataMapper;
            }
            else
            {
                return DataMapper;
            }
        }

        /// <summary>
        /// Executes the <see cref="IQueryRequest"/> using HTTP POST to the Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of each row returned by the query.</typeparam>
        /// <param name="queryRequest">The query request.</param>
        /// <returns></returns>
        /// <remarks>The format for the querying is JSON</remarks>
        private IQueryResult<T> ExecuteQuery<T>(IQueryRequest queryRequest)
        {
            // Cache and clear the current SynchronizationContext before we begin.
            // This eliminates the chance for deadlocks when we wait on an async task sychronously.

            var contextCache = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                return ExecuteQueryAsync<T>(queryRequest, CancellationToken.None).Result;
            }
            finally
            {
                if (contextCache != null)
                {
                    SynchronizationContext.SetSynchronizationContext(contextCache);
                }
            }
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

            FailureCountingUri baseUri;
            if (!TryGetQueryUri(out baseUri))
            {
                Log.Error(ExceptionUtil.EmptyUriTryingSubmitN1qlQuery);
                ProcessError(new InvalidOperationException(ExceptionUtil.EmptyUriTryingSubmitN1QlQuery), queryResult);
                return queryResult;
            }

            ApplyCredentials(queryRequest);

            if (Log.IsDebugEnabled)
            {
                Log.Debug(queryRequest.ToString());
            }

            using (var content = new StringContent(queryRequest.GetFormValuesAsJson(), System.Text.Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    using (var timer = new QueryTimer(queryRequest, new CommonLogStore(Log), ClientConfiguration.EnableQueryTiming))
                    {
                        Log.Trace("Sending query cid{0}: {1}", queryRequest.CurrentContextId, baseUri);
                        var request = await HttpClient.PostAsync(baseUri, content, cancellationToken).ContinueOnAnyContext();
                        using (var response = await request.Content.ReadAsStreamAsync().ContinueOnAnyContext())
                        {
                            queryResult = GetDataMapper(queryRequest).Map<QueryResultData<T>>(response).ToQueryResult();
                            queryResult.Success = queryResult.Status == QueryStatus.Success;
                            queryResult.HttpStatusCode = request.StatusCode;
                            Log.Trace("Received query cid{0}: {1}", queryResult.ClientContextId, queryResult.ToString());
                            timer.ClusterElapsedTime = queryResult.Metrics.ElaspedTime;
                        }
                    }
                    baseUri.ClearFailed();
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
        /// The <see cref="IDataMapper"/> to use for mapping the output stream to a Type.
        /// </summary>
        [Obsolete]
        public IDataMapper DataMapper
        {
            get { return base.DataMapper; }
            set { base.DataMapper = value; }
        }

        /// <summary>
        /// The <see cref="HttpClient"/> to use for the HTTP POST to the Server.
        /// </summary>
        [Obsolete]
        public HttpClient HttpClient
        {
            get { return base.HttpClient; }
            set { base.HttpClient = value; }
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
            baseUri = ConfigContextBase.GetQueryUri(ClientConfiguration.QueryFailedThreshold);
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
