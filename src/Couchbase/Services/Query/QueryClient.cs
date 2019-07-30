using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Services.Query
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

        internal QueryClient(Configuration configuration) : this(
            new HttpClient(new AuthenticatingHttpClientHandler(configuration.UserName, configuration.Password)),
            new JsonDataMapper(new DefaultSerializer()), configuration)
        {
        }

        internal QueryClient(HttpClient httpClient, IDataMapper dataMapper, Configuration configuration)
            : base(httpClient, dataMapper, configuration)
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
        public Task<IQueryResult<T>> QueryAsync<T>(string statement, Action<QueryOptions> configureOptions)
        {
            var options = new QueryOptions();
            configureOptions(options);

            return QueryAsync<T>(statement, options);
        }

        /// <inheritdoc />
        public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options)
        {
            if (!options.IsAdHoc)
            {
                var queryPlan = await PrepareAsync(statement, options).ConfigureAwait(false);
                options.Prepared(queryPlan, statement);
            }

            return await ExecuteQuery<T>(statement, options, options.DataMapper).ConfigureAwait(false);
        }

        internal async Task<QueryPlan> PrepareAsync(string statement, QueryOptions options)
        {
            // try find cached query plan
            if (_queryCache.TryGetValue(statement, out var queryPlan))
            {
                // if an upgrade has happened, don't use query plans that have an encoded plan
                if (EnhancedPreparedStatementsEnabled || string.IsNullOrEmpty(queryPlan.EncodedPlan))
                {
                    return queryPlan;
                }

                // entry is stall, remove from cache
                _queryCache.TryRemove(statement, out _);
            }

            // create prepared statement
            var prepareStatement = statement;
            if (prepareStatement.StartsWith("PREPARE ", StringComparison.InvariantCultureIgnoreCase))
            {
                prepareStatement = "PREPARE " + statement;
            }

            // execute prepare and cache query plan
            var result = await ExecuteQuery<QueryPlan>(prepareStatement, options, _queryPlanDataMapper).ConfigureAwait(false);
            queryPlan = result.Rows.First();

            // make sure we don't store encoded plan if enhanced prepared statements is enabled
            if (EnhancedPreparedStatementsEnabled)
            {
                queryPlan.EncodedPlan = null;
            }

            // add plan to cache
            _queryCache.TryAdd(statement, queryPlan);

            return queryPlan;
        }

        internal async Task<IQueryResult<T>> ExecuteQuery<T>(string statement, QueryOptions options, IDataMapper dataMapper)
        {
            // try get Query node
            if (!Configuration.GlobalNodes.TryGetRandom(x => x.HasQuery(), out var node))
            {
                const string noNodeAvailableMessage = "Unable to locate query node to submit query to.";
                Logger.LogError(noNodeAvailableMessage);
                throw new ServiceNotAvailableException(ServiceType.Query);
            }

            options.Statement(statement);
            var body = options.GetFormValuesAsJson();

            StreamingQueryResult<T> queryResult;
            using (var content = new StringContent(body, System.Text.Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    var response = await HttpClient.PostAsync(node.QueryUri, content, options.CancellationToken)
                        .ConfigureAwait(false);

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
