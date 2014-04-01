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
    internal class QueryClient : IQueryClient
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();

        public QueryClient(HttpClient httpClient, IDataMapper dataMapper)
        {
            HttpClient = httpClient;
            DataMapper = dataMapper;
        }

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

        public IDataMapper DataMapper { get; set; }

        public HttpClient HttpClient { get; set; }
    }
}
