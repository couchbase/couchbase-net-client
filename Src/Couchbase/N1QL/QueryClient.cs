using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Views;

namespace Couchbase.N1QL
{
    /// <summary>
    /// A <see cref="IViewClient"/> implementation for executing N1QL queries against a Couchbase Server.
    /// </summary>
    internal class QueryClient : IQueryClient
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper)
        {
            HttpClient = httpClient;
            DataMapper = dataMapper;
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
            IQueryResult<T> queryResult = new QueryResult<T>();

            var content = new StringContent(query);
            var postTask = HttpClient.PostAsync(server, content);
            try
            {
                postTask.Wait();
                var postResult = postTask.Result;

                var readTask = postResult.Content.ReadAsStreamAsync();
                readTask.Wait();

                queryResult = DataMapper.Map<QueryResult<T>>(readTask.Result);
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Log.Error(e);
                    return true;
                });
            }
            return queryResult;
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
