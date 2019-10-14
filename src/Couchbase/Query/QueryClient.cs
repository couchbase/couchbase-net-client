using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Query
{
    /// <summary>
    /// A <see cref="QueryClient" /> implementation for executing N1QL queries against a Couchbase Server.
    /// </summary>
    internal class QueryClient : HttpServiceBase, IQueryClient
    {
        internal const string Error5000MsgQueryPortIndexNotFound = "queryport.indexNotFound";
        private static readonly ILogger Logger = LogManager.CreateLogger<QueryClient>();
        private readonly ConcurrentDictionary<string, QueryPlan> _queryCache = new ConcurrentDictionary<string, QueryPlan>();
        private readonly IDataMapper _queryPlanDataMapper = new JsonDataMapper(new DefaultSerializer());
        internal bool EnhancedPreparedStatementsEnabled;

        internal QueryClient(ClusterContext context) : this(
            new HttpClient(new AuthenticatingHttpClientHandler(context.ClusterOptions.UserName, context.ClusterOptions.Password)),
            new JsonDataMapper(new DefaultSerializer()), context)
        {
        }

        internal QueryClient(HttpClient httpClient, IDataMapper dataMapper, ClusterContext context)
            : base(httpClient, dataMapper, context)
        {
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
            // does this query use a prepared plan?
            if (options.IsAdHoc)
            {
                // don't use prepared plan, execute query directly
                options.Statement(statement);
                return await ExecuteQuery<T>(options, options.DataMapper).ConfigureAwait(false);
            }

            // try find cached query plan
            if (_queryCache.TryGetValue(statement, out var queryPlan))
            {
                // if an upgrade has happened, don't use query plans that have an encoded plan
                if (!EnhancedPreparedStatementsEnabled || string.IsNullOrWhiteSpace(queryPlan.EncodedPlan))
                {
                    // plan is valid, execute query with it
                    options.Prepared(queryPlan, statement);
                    return await ExecuteQuery<T>(options, options.DataMapper).ConfigureAwait(false);
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
                // execute combined prepare & execute query
                options.AutoExecute(true);
                var result = await ExecuteQuery<T>(options, options.DataMapper).ConfigureAwait(false);

                // add/replace query plan name in query cache
                if (result is StreamingQueryResult<T> streamingResult) // NOTE: hack to not make 'PreparedPlanName' property public
                {
                    var plan = new QueryPlan {Name = streamingResult.PreparedPlanName, Text = statement};
                    _queryCache.AddOrUpdate(statement, plan, (k, p) => plan);
                }

                return result;
            }

            // older style, prepare then execute
            var preparedResult = await ExecuteQuery<QueryPlan>(options, _queryPlanDataMapper).ConfigureAwait(false);
            queryPlan = preparedResult.First();

            // add plan to cache and execute
            _queryCache.TryAdd(statement, queryPlan);
            options.Prepared(queryPlan, statement);

            // execute query using plan
            return await ExecuteQuery<T>(options, options.DataMapper).ConfigureAwait(false);
        }

        private async Task<IQueryResult<T>> ExecuteQuery<T>(QueryOptions options, IDataMapper dataMapper)
        {
            // try get Query node
            var node = Context.GetRandomNodeForService(ServiceType.Query);
            var body = options.GetFormValuesAsJson();

            StreamingQueryResult<T> queryResult;
            using (var content = new StringContent(body, System.Text.Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    var response = await HttpClient.PostAsync(node.QueryUri, content, options.CancellationToken).ConfigureAwait(false);
                    var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    queryResult = new StreamingQueryResult<T>
                    {
                        ResponseStream = stream,
                        HttpStatusCode = response.StatusCode,
                        Success = response.StatusCode == HttpStatusCode.OK
                    };

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        //read the header and stop when we reach the queried rows
                        queryResult.ReadToRows();
                        if (response.StatusCode != HttpStatusCode.OK || queryResult.Status != QueryStatus.Success)
                        {
                            throw new QueryException(queryResult.Message,
                                queryResult.Status,
                                queryResult.Errors);
                        }
                    }
                }
                catch (TaskCanceledException e)
                {
                    throw new TimeoutException("The request has timed out.", e);
                }
            }

            return queryResult;
        }

        internal void UpdateClusterCapabilities(ClusterCapabilities clusterCapabilities)
        {
            if (!EnhancedPreparedStatementsEnabled && clusterCapabilities.EnhancedPreparedStatementsEnabled)
            {
                EnhancedPreparedStatementsEnabled = true;
                Logger.LogInformation("Enabling Enhanced Prepared Statements");
            }
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
