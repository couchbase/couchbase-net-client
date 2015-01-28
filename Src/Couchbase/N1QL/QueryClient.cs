using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Views;
using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    /// <summary>
    /// A <see cref="IViewClient"/> implementation for executing N1QL queries against a Couchbase Server.
    /// </summary>
    public class QueryClient : IQueryClient
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper, ClientConfiguration clientConfig)
        {
            HttpClient = httpClient;
            DataMapper = dataMapper;
            _clientConfig = clientConfig;
        }

        /// <summary>
        /// Executes an ad-hoc N1QL query against a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type to cast the resulting rows to.</typeparam>
        /// <param name="server">The <see cref="Uri"/> of the server.</param>
        /// <param name="query">A string containing a N1QL query.</param>
        /// <returns>An <see cref="IQueryResult{T}"/> implementation representing the results of the query.</returns>
        public async Task<IQueryResult<T>> QueryAsync<T>(Uri server, string query)
        {
            var queryResult = new QueryResult<T>();
            var content = new StringContent(query);
            try
            {
                var request = await HttpClient.PostAsync(server, content);
                var response = await request.Content.ReadAsStreamAsync();

                queryResult = DataMapper.Map<QueryResult<T>>(response);
                queryResult.Success = queryResult.Status == QueryStatus.Success;
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Log.Error(e);
                    ProcessError(e, queryResult);
                    return true;
                });
            }
            catch (Exception e)
            {
                ProcessError(e, queryResult);
                Log.Error(e);
            }
            return queryResult;
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
            var queryResult = new QueryResult<T>();

            var content = new StringContent(query);
            var postTask = HttpClient.PostAsync(server, content);
            try
            {
                postTask.Wait();
                var postResult = postTask.Result;

                var readTask = postResult.Content.ReadAsStreamAsync();
                readTask.Wait();

                queryResult = DataMapper.Map<QueryResult<T>>(readTask.Result);
                queryResult.Success = queryResult.Status == QueryStatus.Success;
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Log.Error(e);
                    ProcessError(e, queryResult);
                    return true;
                });
            }
            catch (Exception e)
            {
                ProcessError(e, queryResult);
                Log.Error(e);
            }
            return queryResult;
        }

        void CachePreparedStatement(IQueryRequest queryRequest)
        {
            var preparable = queryRequest as IPreparable;
            if (preparable != null)
            {
                if (!preparable.HasPrepared)
                {
                    var queryResult = Post<dynamic>(queryRequest);
                    if (queryResult.Success)
                    {
                        var statement = queryResult.Rows.First();
                        statement = EncodeParameter(statement);
                        preparable.CachePreparedStatement(statement);
                    }
                }
            }
        }

        static string EncodeParameter(object parameter)
        {
            return Uri.EscapeDataString(JsonConvert.SerializeObject(parameter));
        }

        public IQueryResult<T> Query<T>(IQueryRequest queryRequest)
        {
            if (queryRequest.IsPrepared)
            {
                CachePreparedStatement(queryRequest);
            }
            var requestUri = queryRequest.GetRequestUri();

            var queryResult = queryRequest.IsPost ?
                Post<T>(queryRequest) : Get<T>(requestUri);

            return queryResult;
        }

        public Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest)
        {
            var requestUri = queryRequest.GetRequestUri();

            var queryResult = queryRequest.IsPost ?
                PostAsync<T>(queryRequest) : GetAsync<T>(requestUri);

            return queryResult;
        }

        IQueryResult<T> Post<T>(IQueryRequest queryRequest)
        {
            var queryResult = new QueryResult<T>();
            try
            {
                var request = WebRequest.Create(queryRequest.GetBaseUri());
                request.Timeout = _clientConfig.ViewRequestTimeout;
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";

                var bytes = System.Text.Encoding.UTF8.GetBytes(queryRequest.GetQueryParameters());
                request.ContentLength = bytes.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                var response = request.GetResponse();
                using (var stream = response.GetResponseStream())
                {
                    queryResult = DataMapper.Map<QueryResult<T>>(stream);
                    queryResult.Success = queryResult.Status == QueryStatus.Success;
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var stream = e.Response.GetResponseStream();
                    queryResult = DataMapper.Map<QueryResult<T>>(stream);
                }
                queryResult.Exception = e;
                Log.Error(e);
            }
            catch (Exception e)
            {
                ProcessError(e, queryResult);
                Log.Error(e);
            }
            return queryResult;
        }

        private async Task<IQueryResult<T>> PostAsync<T>(IQueryRequest queryRequest)
        {
            var queryResult = new QueryResult<T>();
            var content = new FormUrlEncodedContent(queryRequest.GetFormValues());
            try
            {
                var request = await HttpClient.PostAsync(queryRequest.GetBaseUri(), content);
                using (var response = await request.Content.ReadAsStreamAsync())
                {
                    queryResult = DataMapper.Map<QueryResult<T>>(response);
                    queryResult.Success = queryResult.Status == QueryStatus.Success;
                }
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Log.Error(e);
                    ProcessError(e, queryResult);
                    return true;
                });
            }
            return queryResult;
        }

        IQueryResult<T> Get<T>(Uri requestUri)
        {
            var queryResult = new QueryResult<T>();
            try
            {
                var request = WebRequest.Create(requestUri);
                request.Timeout = _clientConfig.ViewRequestTimeout;
                request.Method = "GET";

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    using (var stream = response.GetResponseStream())
                    {
                        queryResult = DataMapper.Map<QueryResult<T>>(stream);
                        queryResult.Success = response.StatusCode == HttpStatusCode.OK;
                        response.Close();
                    }
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var stream = e.Response.GetResponseStream();
                    queryResult = DataMapper.Map<QueryResult<T>>(stream);
                }
                queryResult.Exception = e;
                Log.Error(e);
            }
            catch (Exception e)
            {
                ProcessError(e, queryResult);
                Log.Error(e);
            }
            return queryResult;
        }

        async Task<IQueryResult<T>> GetAsync<T>(Uri requestUri)
        {
            var queryResult = new QueryResult<T>();
            try
            {
                var request = await HttpClient.GetAsync(requestUri);
                using (var response = await request.Content.ReadAsStreamAsync())
                {
                    queryResult = DataMapper.Map<QueryResult<T>>(response);
                    queryResult.Success = queryResult.Status == QueryStatus.Success;
                }
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Log.Error(e);
                    ProcessError(e, queryResult);
                    return true;
                });
            }
            catch (Exception e)
            {
                ProcessError(e, queryResult);
                Log.Error(e);
            }
            return queryResult;
        }

        private static void ProcessError<T>(Exception ex, QueryResult<T> queryResult)
        {
            const string message = "Check Exception and Error fields for details.";
            queryResult.Status = QueryStatus.Fatal;
            queryResult.Success = false;
            queryResult.Message = message;
            queryResult.Exception = ex;
        }

        /// <summary>
        /// The <see cref="IDataMapper"/> to use for mapping the output stream to a Type.
        /// </summary>
        public IDataMapper DataMapper { get; set; }

        /// <summary>
        /// The <see cref="HttpClient"/> to use for the HTTP POST to the Server.
        /// </summary>
        public HttpClient HttpClient { get; set; }
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