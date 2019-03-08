using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

namespace Couchbase.Services.Query
{
    /// <summary>
    /// A <see cref="QueryClient" /> implementation for executing N1QL queries against a Couchbase Server.
    /// </summary>
    internal class QueryClient : HttpServiceBase, IQueryClient
    {
        internal static readonly string Error5000MsgQueryportIndexNotFound = "queryport.indexNotFound";

        public QueryClient(IConfiguration configuration) : this(
            new HttpClient(new AuthenticatingHttpClientHandler(configuration.UserName, configuration.Password)),
            new JsonDataMapper(new DefaultSerializer()), configuration)
        {
        }

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper, IConfiguration configuration)
            : base(httpClient, dataMapper, configuration)
        {
        }

        public int InvalidateQueryCache()
        {
            throw new NotImplementedException();
        }

        public IQueryResult<QueryPlan> Prepare(string statement, IQueryOptions toPrepare)
        {
            throw new NotImplementedException();
        }

        public Task<IQueryResult<T>> QueryAsync<T>(string statment, IQueryOptions options)
        {
            return QueryAsync<T>(statment, options, CancellationToken.None);
        }

        public async Task<IQueryResult<T>> QueryAsync<T>(string statement, IQueryOptions options, CancellationToken cancellationToken)
        {
            var uriBuilder = new UriBuilder(Configuration.Servers.GetRandom())
            {
                Scheme = "http",
                Path = "/query",
                Port = 8093
            };

            options.Statement(statement);
            var body = options.GetFormValuesAsJson();

            StreamingQueryResult<T> queryResult = null;
            using (var content = new StringContent(body, System.Text.Encoding.UTF8, MediaType.Json))
            {
                try
                {
                    var response = await HttpClient.PostAsync(uriBuilder.Uri, content, cancellationToken)
                        .ConfigureAwait(false);

                    var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    queryResult = new StreamingQueryResult<T>
                    {
                        ResponseStream = stream,
                        HttpStatusCode = response.StatusCode,
                        Success = response.StatusCode == HttpStatusCode.OK,
                    };

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        //read the header and stop when we reach the queried rows
                        queryResult.ReadToRows();

                        //A problem with the HTTP request itself
                        if (response.StatusCode == HttpStatusCode.BadRequest)
                        {
                            throw new QueryErrorException(response.ReasonPhrase,
                                queryResult.Status,
                                queryResult.Errors);
                        }

                        //A problem with the service itself
                        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            throw new QueryServiceException(response.ReasonPhrase,
                                queryResult.Status, 
                                queryResult.Errors);
                        }

                        //A problem with the query iteself
                        if (queryResult.Status != QueryStatus.Success)
                        {
                            throw new QueryException(queryResult.Message,
                                queryResult.Status,
                                queryResult.Errors);
                        }
                    }
                }
                catch (TaskCanceledException e)
                {
                    throw new TimeoutException("The request has timed out.");
                }
            }

            return queryResult;
        }

        /// <summary>
        /// Returns the <see cref="IDataMapper"/> to use for a given <see cref="IQueryOptions"/>
        /// </summary>
        /// <param name="queryOptions">Request to get the <see cref="IDataMapper"/> for</param>
        /// <returns><see cref="IDataMapper"/> to use for the request</returns>
        internal IDataMapper GetDataMapper(IQueryOptions queryOptions)
        {
            if (queryOptions is IQueryRequestWithDataMapper requestWithMapper)
            {
                return requestWithMapper.DataMapper ?? DataMapper;
            }

            return DataMapper;
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
