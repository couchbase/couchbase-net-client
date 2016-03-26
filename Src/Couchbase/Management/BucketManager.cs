using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Management.Indexes;
using Couchbase.N1QL;
using Couchbase.Utils;
using Encoding = System.Text.Encoding;

namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Bucket.
    /// </summary>
    public class BucketManager : IBucketManager
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly string _username;
        private readonly string _password;
        private readonly IBucket _bucket;

        internal BucketManager(IBucket bucket, ClientConfiguration clientConfig, HttpClient httpClient, IDataMapper mapper, string username, string password)
        {
            _bucket = bucket;
            BucketName = bucket.Name;
            _clientConfig = clientConfig;
            Mapper = mapper;
            HttpClient = httpClient;
            _password = password;
            _username = username;
        }

        /// <summary>
        /// N1QL statements for creating and dropping indexes on the current bucket
        /// </summary>
        private static class Statements
        {
            public static string ListIndexes = "SELECT i.* FROM system:indexes AS i WHERE i.keyspace_id=\"{0}\" AND `using`=\"gsi\";";
            public static string CreatePrimaryIndex = "CREATE PRIMARY INDEX ON {0} USING GSI WITH {{\"defer_build\":{1}}};";
            public static string CreateNamedPrimaryIndex = "CREATE PRIMARY INDEX {0} ON {1} USING GSI WITH {{\"defer_build\":{2}}};";
            public static string DropPrimaryIndex = "DROP PRIMARY INDEX ON {0} USING GSI;";
            public static string DropNamedPrimaryIndex = "DROP INDEX {0}.{1} USING GSI;";
            public static string DropIndex = "DROP INDEX {0}.{1} USING GSI;";
            public static string CreateIndexWithFields = "CREATE INDEX {0} ON {1}({2}) USING GSI WITH {{\"defer_build\":{3}}};";
            public static string BuildIndexes = "BUILD INDEX ON {0}({1}) USING GSI;";
        }

        public HttpClient HttpClient { get; private set; }

        public IDataMapper Mapper { get; private set; }

        /// <summary>
        /// The name of the Bucket.
        /// </summary>
        public string BucketName { get; private set; }

        /// <summary>
        /// Lists the indexes for the current <see cref="IBucket" />.
        /// </summary>
        /// <returns></returns>
        public IndexResult ListIndexes()
        {
            var request = new QueryRequest(string.Format(Statements.ListIndexes, BucketName));
            var result = _bucket.Query<IndexInfo>(request);

            return new IndexResult
            {
                Value = result.Rows,
                Exception = result.Exception,
                Message = result.Message,
                Success = result.Success
            };
        }

        /// <summary>
        /// Lists the indexes for a the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <returns></returns>
        public async Task<IndexResult> ListIndexesAsync()
        {
            var request = new QueryRequest(string.Format(Statements.ListIndexes, BucketName));
            var result = await _bucket.QueryAsync<IndexInfo>(request);

            return new IndexResult
            {
                Value = result.Rows,
                Exception = result.Exception,
                Message = result.Message,
                Success = result.Success
            };
        }

        /// <summary>
        /// Creates the primary index for the current bucket if it doesn't already exist.
        /// </summary>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        public IResult CreatePrimaryIndex(bool defer = false)
        {
            var statement = string.Format(Statements.CreatePrimaryIndex,
                BucketName.N1QlEscape(), defer.ToString().ToLower(CultureInfo.CurrentCulture));

            var result = ExecuteIndexRequest(statement);
            return result;
        }

        /// <summary>
        /// Creates a primary index on the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public Task<IResult> CreatePrimaryIndexAsync(bool defer = false)
        {
            var statement = string.Format(Statements.CreatePrimaryIndex,
                BucketName.N1QlEscape(), defer.ToString().ToLower(CultureInfo.CurrentCulture));

            var result = ExecuteIndexRequestAsync(statement);
            return result;
        }

        /// <summary>
        /// Creates a named primary index on the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <param name="customName">The name of the custom index.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public Task<IResult> CreateNamedPrimaryIndexAsync(string customName, bool defer = false)
        {
            var statement = string.Format(Statements.CreateNamedPrimaryIndex,
                customName.N1QlEscape(), BucketName.N1QlEscape(), defer.ToString().ToLower(CultureInfo.CurrentCulture));

            var result = ExecuteIndexRequestAsync(statement);
            return result;
        }

        /// <summary>
        /// Creates a secondary index with optional fields asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <param name="fields">The fields to index on.</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public Task<IResult> CreateIndexAsync(string indexName, bool defer = false, params string[] fields)
        {
            var fieldStr = string.Empty;
            if (fields != null)
            {
                fieldStr = fields.ToDelimitedN1QLString(',');
            }

            var statement = string.Format(Statements.CreateIndexWithFields,
               indexName.N1QlEscape(), BucketName.N1QlEscape(), fieldStr, defer.ToString().ToLower(CultureInfo.CurrentCulture));

            var result = ExecuteIndexRequestAsync(statement);
            return result;
        }

        /// <summary>
        /// Drops the primary index of the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public Task<IResult> DropPrimaryIndexAsync()
        {
            var statement = string.Format(Statements.DropPrimaryIndex, BucketName.N1QlEscape());
            var result = ExecuteIndexRequestAsync(statement);
            return result;
        }

        /// <summary>
        /// Drops the named primary index on the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <param name="customName">Name of the primary index to drop.</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public Task<IResult> DropNamedPrimaryIndexAsync(string customName)
        {
            var statement = string.Format(Statements.DropNamedPrimaryIndex, BucketName.N1QlEscape(), customName.N1QlEscape());
            var result = ExecuteIndexRequestAsync(statement);
            return result;
        }

        /// <summary>
        /// Drops an index by name asynchronously.
        /// </summary>
        /// <param name="name">The name of the index to drop.</param>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public Task<IResult> DropIndexAsync(string name)
        {
            var statement = string.Format(Statements.DropIndex, BucketName.N1QlEscape(), name.N1QlEscape());
            var result = ExecuteIndexRequestAsync(statement);
            return result;
        }

        /// <summary>
        /// Builds any indexes that have been created with the "defered" flag and are still in the "pending" state asynchronously.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public Task<IResult[]> BuildDeferredIndexesAsync()
        {
            var tasks = new List<Task<IResult>>();
            var indexes = ListIndexes();

            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var index in indexes.Where(x => x.State == "pending" || x.State == "deferred"))
            {
                var statement = string.Format(Statements.BuildIndexes, BucketName.N1QlEscape(), index.Name.N1QlEscape());
                var task = ExecuteIndexRequestAsync(statement);
                tasks.Add(task);
            }
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Watches the indexes asynchronously.
        /// </summary>
        /// <param name="watchList">The watch list.</param>
        /// <param name="watchPrimary">if set to <c>true</c> [watch primary].</param>
        /// <param name="watchTimeout">The watch timeout.</param>
        /// <param name="watchTimeUnit">The watch time unit.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task<IResult<List<IndexInfo>>> WatchIndexesAsync(List<string> watchList, bool watchPrimary, long watchTimeout, TimeSpan watchTimeUnit)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a primary index on the current <see cref="IBucket" /> reference.
        /// </summary>
        /// <param name="customName">The name of the index.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        public IResult CreateNamedPrimaryIndex(string customName, bool defer = false)
        {
            var statement = string.Format(Statements.CreateNamedPrimaryIndex,
                customName.N1QlEscape(), BucketName.N1QlEscape(), defer.ToString().ToLower(CultureInfo.CurrentCulture));

            var result = ExecuteIndexRequest(statement);
            return result;
        }

        /// <summary>
        /// Creates a secondary index on the current <see cref="IBucket" /> reference.
        /// </summary>
        /// <param name="indexName">Name of the index to create.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <param name="fields">The fields to index on.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        public IResult CreateIndex(string indexName, bool defer = false, params string[] fields)
        {
            var fieldStr = fields.ToDelimitedN1QLString(',');
            var statement = string.Format(Statements.CreateIndexWithFields,
                indexName.N1QlEscape(), BucketName.N1QlEscape(), fieldStr, defer.ToString().ToLower(CultureInfo.CurrentCulture));

            var result = ExecuteIndexRequest(statement);
            return result;
        }

        /// <summary>
        /// Drops the primary index on the current <see cref="IBucket" />.
        /// </summary>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        public IResult DropPrimaryIndex()
        {
            var statement = string.Format(Statements.DropPrimaryIndex, BucketName.N1QlEscape());
            var result = ExecuteIndexRequest(statement);
            return result;
        }

        /// <summary>
        /// Drops the named primary index if it exists on the current <see cref="IBucket" />.
        /// </summary>
        /// <param name="customName">Name of primary index.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        public IResult DropNamedPrimaryIndex(string customName)
        {
            var statement = string.Format(Statements.DropNamedPrimaryIndex, BucketName.N1QlEscape(), customName.N1QlEscape());
            var result = ExecuteIndexRequest(statement);
            return result;
        }

        /// <summary>
        /// Drops a secondary index on the current <see cref="IBucket" /> reference.
        /// </summary>
        /// <param name="name">The name of the secondary index to drop.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        public IResult DropIndex(string name)
        {
            var statement = string.Format(Statements.DropIndex, BucketName.N1QlEscape(), name.N1QlEscape());
            var result = ExecuteIndexRequest(statement);
            return result;
        }

        /// <summary>
        /// Builds any indexes that have been created with the "defer" flag and are still in the "pending" state on the current <see cref="IBucket" />.
        /// </summary>
        /// <returns>
        /// An <see cref="IList{IResult}" /> with the status for each index built.
        /// </returns>
        public IList<IResult> BuildDeferredIndexes()
        {
            var results = new List<IResult>();
            var indexes = ListIndexes();

            // ReSharper disable once LoopCanBeConvertedToQuery
            var deferredIndexes = indexes.Where(x => x.State == "pending" || x.State == "deferred").ToList();
            foreach (var index in deferredIndexes)
            {
                var statement = string.Format(Statements.BuildIndexes, BucketName.N1QlEscape(), index.Name.N1QlEscape());
                var result = ExecuteIndexRequest(statement);
                results.Add(result);
            }
            return results;
        }

        /// <summary>
        /// Watches the indexes.
        /// </summary>
        /// <param name="watchList">The watch list.</param>
        /// <param name="watchPrimary">if set to <c>true</c> [watch primary].</param>
        /// <param name="watchTimeout">The watch timeout.</param>
        /// <param name="watchTimeUnit">The watch time unit.</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public IResult<List<IndexInfo>> WatchIndexes(List<string> watchList, bool watchPrimary, long watchTimeout, TimeSpan watchTimeUnit)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes the index request asynchronously.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns></returns>
        protected virtual async Task<IResult> ExecuteIndexRequestAsync(string statement)
        {
            var request = new QueryRequest(statement);
            return await _bucket.QueryAsync<IndexInfo>(request).ContinueOnAnyContext();
        }

        /// <summary>
        /// Executes the index request syncronously.
        /// </summary>
        /// <param name="statement">The statement.</param>
        /// <returns></returns>
        protected virtual IResult ExecuteIndexRequest(string statement)
        {
            var request = new QueryRequest(statement);
            return _bucket.Query<IndexInfo>(request);
        }

        /// <summary>
        /// Inserts a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult InsertDesignDocument(string designDocName, string designDoc)
        {
            IResult result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "PUT";
                request.Accept = request.ContentType = "application/json";
                request.Credentials = new NetworkCredential(_username, _password);

                var bytes = System.Text.Encoding.UTF8.GetBytes(designDoc);
                request.ContentLength = bytes.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    using (var reqStream = response.GetResponseStream())
                    {
                        result = GetResult(response.StatusCode, reqStream);
                    }
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var res = (HttpWebResponse)e.Response;
                    var stream = e.Response.GetResponseStream();
                    result = GetResultAsString(res.StatusCode, stream);
                }
                else result = WebRequestError(e);
                Log.Error(e);
            }
            catch (Exception e)
            {
                result = WebRequestError(e);
                Log.Error(e);
            }
            return result;
        }

        /// <summary>
        /// Inserts a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> InsertDesignDocumentAsync(string designDocName, string designDoc)
        {
            IResult result;
            try
            {
                using (var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential(_username, _password)
                })
                {
                    using (var client = new HttpClient(handler))
                    {
                        var server = _clientConfig.Servers.First();
                        const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                        var protocol = UseSsl() ? "https" : "http";
                        var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                        var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                        var contentType = new MediaTypeWithQualityHeaderValue("application/json");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;

                        var request = new HttpRequestMessage(HttpMethod.Put, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        request.Content = new StringContent(designDoc);
                        request.Content.Headers.ContentType = contentType;

                        var task = client.PutAsync(uri, request.Content);
                        await task;

                        result = await GetResult(task.Result);
                    }
                }
            }
            catch (AggregateException e)
            {
                result = new DefaultResult(false, e.Message, e);
                Log.Error(e);
            }
            return result;
        }

        /// <summary>
        /// Updates a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult UpdateDesignDocument(string designDocName, string designDoc)
        {
            return InsertDesignDocument(designDocName, designDoc);
        }

        /// <summary>
        /// Updates a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public Task<IResult> UpdateDesignDocumentAsync(string designDocName, string designDoc)
        {
            return InsertDesignDocumentAsync(designDocName, designDoc);
        }

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A design document object.</returns>
        public IResult<string> GetDesignDocument(string designDocName)
        {
            IResult<string> result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                var request = WebRequest.Create(uri);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Credentials = new NetworkCredential(_username, _password);

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    using (var reqStream = response.GetResponseStream())
                    {
                        result = GetResultAsString(response.StatusCode, reqStream);
                    }
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var res = (HttpWebResponse)e.Response;
                    var stream = e.Response.GetResponseStream();
                    result = GetResultAsString(res.StatusCode, stream);
                }
                else result = new DefaultResult<string>(false, e.Message, e);
                Log.Error(e);
            }
            catch (Exception e)
            {
                result = new DefaultResult<string>(false, e.Message, e);
                Log.Error(e);
            }
            return result;
        }

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A design document object.</returns>
        public async Task<IResult<string>> GetDesignDocumentAsync(string designDocName)
        {
            IResult<string> result;
            try
            {
                using (var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential(_username, _password)
                })
                {
                    using (var client = new HttpClient(handler))
                    {
                        var server = _clientConfig.Servers.First();
                        const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                        var protocol = UseSsl() ? "https" : "http";
                        var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                        var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                        var contentType = new MediaTypeWithQualityHeaderValue("application/json");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;
                        var request = new HttpRequestMessage(HttpMethod.Get, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        var task = client.GetAsync(uri);
                        await task;

                        var taskResult = task.Result;
                        var content = taskResult.Content;
                        var stream = content.ReadAsStreamAsync();
                        await stream;

                        result = await GetResultAsString(task.Result);
                    }
                }
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult<string>(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Removes a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult RemoveDesignDocument(string designDocName)
        {
            IResult result;
            try
            {

                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));
                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "DELETE";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    using (var reqStream = response.GetResponseStream())
                    {
                        var message = GetString(reqStream);
                        result = new DefaultResult(response.StatusCode == HttpStatusCode.OK, message, null);
                    }
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var res = (HttpWebResponse)e.Response;
                    var stream = e.Response.GetResponseStream();
                    result = GetResultAsString(res.StatusCode, stream);
                }
                else result = WebRequestError(e);
                Log.Error(e);
            }
            catch (Exception e)
            {
                result = WebRequestError(e);
                Log.Error(e);
            }
            return result;
        }
        /// <summary>
        /// Removes a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> RemoveDesignDocumentAsync(string designDocName)
        {
            IResult result;
            try
            {
                using (var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential(_username, _password)
                })
                {
                    using (var client = new HttpClient(handler))
                    {
                        var server = _clientConfig.Servers.First();
                        const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                        var protocol = UseSsl() ? "https" : "http";
                        var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                        var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;
                        var request = new HttpRequestMessage(HttpMethod.Delete, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        var task = client.DeleteAsync(uri);
                        await task;

                        var taskResult = task.Result;
                        var content = taskResult.Content;
                        var stream = content.ReadAsStreamAsync();
                        await stream;

                        result = await GetResult(task.Result);
                    }
                }
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Lists all existing design documents.
        /// </summary>
        /// <param name="includeDevelopment">Whether or not to show development design documents in the results.</param>
        /// <returns>The design document as a string.</returns>
        [Obsolete("Note that the overload which takes an 'includeDevelopment' is obsolete; the method will ignore the parameter value if passed.")]
        public IResult<string> GetDesignDocuments(bool includeDevelopment = false)
        {
            IResult<string> result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/pools/default/buckets/{3}/ddocs";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName));

                var request = WebRequest.Create(uri);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Credentials = new NetworkCredential(_username, _password);

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    using (var reqStream = response.GetResponseStream())
                    {
                        result = GetResultAsString(response.StatusCode, reqStream);
                    }
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var res = (HttpWebResponse)e.Response;
                    var stream = e.Response.GetResponseStream();
                    result = GetResultAsString(res.StatusCode, stream);
                }
                else result = new DefaultResult<string>(false, e.Message, e);
                Log.Error(e);
            }
            catch (Exception e)
            {
                result = new DefaultResult<string>(false, e.Message, e);
                Log.Error(e);
            }
            return result;
        }

        /// <summary>
        /// Lists all existing design documents.
        /// </summary>
        /// <param name="includeDevelopment">Whether or not to show development design documents in the results.</param>
        /// <returns>The design document as a string.</returns>
        public async Task<IResult<string>> GetDesignDocumentsAsync(bool includeDevelopment = false)
        {
            IResult<string> result;
            try
            {
                using (var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential(_username, _password)
                })
                {
                    using (var client = new HttpClient(handler))
                    {
                        var server = _clientConfig.Servers.First();
                        const string api = "{0}://{1}:{2}/pools/default/buckets/{3}/ddocs";
                        var protocol = UseSsl() ? "https" : "http";
                        var port = UseSsl() ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
                        var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName));

                        var contentType = new MediaTypeWithQualityHeaderValue("application/json");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;
                        var request = new HttpRequestMessage(HttpMethod.Get, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        var task = client.GetAsync(uri);
                        await task;

                        result = await GetResultAsString(task.Result);
                    }
                }
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult<string>(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Destroys all documents stored within a bucket.  This functionality must also be enabled within the server-side bucket settings for safety reasons.
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating success.</returns>
        public IResult Flush()
        {
            IResult result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/pools/default/buckets/{3}/controller/doFlush";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName));

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "POST";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);
                var formData = new Dictionary<string, object>
                              {
                                  {"user", _username},
                                  {"password", _password}
                              };
                var bytes = System.Text.Encoding.UTF8.GetBytes(PostDataDicToString(formData));
                request.ContentLength = bytes.Length;

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(bytes, 0, bytes.Length);
                }

                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    using (var reqStream = response.GetResponseStream())
                    {
                        var message = GetString(reqStream);
                        result = new DefaultResult(response.StatusCode == HttpStatusCode.OK, message, null);
                    }
                }
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var stream = e.Response.GetResponseStream();
                    result = WebRequestError(e, GetString(stream));
                }
                else result = WebRequestError(e);
                Log.Error(e);
            }
            catch (Exception e)
            {
                result = WebRequestError(e);
                Log.Error(e);
            }
            return result;
        }

        /// <summary>
        /// Destroys all documents stored within a bucket.  This functionality must also be enabled within the server-side bucket settings for safety reasons.
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating success.</returns>
        public async Task<IResult> FlushAsync()
        {
            IResult result;
            try
            {
                using (var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential(_username, _password)
                })
                {
                    using (var client = new HttpClient(handler))
                    {
                        var server = _clientConfig.Servers.First();
                        const string api = "{0}://{1}:{2}/pools/default/buckets/{3}/controller/doFlush";
                        var protocol = UseSsl() ? "https" : "http";
                        var port = UseSsl() ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
                        var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName));

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;
                        var request = new HttpRequestMessage(HttpMethod.Post, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            {"user", _username},
                            {"password", _password}
                        });
                        request.Content.Headers.ContentType = contentType;

                        var task = client.PostAsync(uri, request.Content);
                        await task;

                        result = await GetResult(task.Result);
                    }
                }
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        private static string GetString(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }

        private async Task<IResult<string>> GetResultAsString(HttpResponseMessage httpResponseMessage)
        {
            var content = httpResponseMessage.Content;
            var stream = content.ReadAsStreamAsync();
            await stream;

            var body = GetString(stream.Result);
            var result = new DefaultResult<string>
            {
                Message = httpResponseMessage.IsSuccessStatusCode ? "success" : body,
                Success = httpResponseMessage.IsSuccessStatusCode,
                Value = httpResponseMessage.IsSuccessStatusCode ? body : null
            };
            Log.Debug(m => m("{0}", body));
            return result;
        }

        private IResult<string> GetResultAsString(HttpStatusCode statusCode, Stream stream)
        {
            var body = GetString(stream);
            var result = new DefaultResult<string>
            {
                Message = IsSuccessStatusCode(statusCode) ? "success" : body,
                Success = IsSuccessStatusCode(statusCode),
                Value = IsSuccessStatusCode(statusCode) ? body : null
            };
            Log.Debug(m => m("{0}", body));
            return result;
        }

        private async Task<IResult> GetResult(HttpResponseMessage httpResponseMessage)
        {
            var content = httpResponseMessage.Content;
            var stream = content.ReadAsStreamAsync();
            await stream;

            var body = GetString(stream.Result);
            var result = new DefaultResult
            {
                Message = httpResponseMessage.IsSuccessStatusCode ? "success" : body,
                Success = httpResponseMessage.IsSuccessStatusCode,
            };
            Log.Debug(m => m("{0}", body));
            return result;
        }

        private IResult GetResult(HttpStatusCode statusCode, Stream stream)
        {
            var body = GetString(stream);
            var result = new DefaultResult
            {
                Message = IsSuccessStatusCode(statusCode) ? "success" : body,
                Success = IsSuccessStatusCode(statusCode),
            };
            Log.Debug(m => m("{0}", body));
            return result;
        }

        private bool IsSuccessStatusCode(HttpStatusCode statusCode)
        {
            return statusCode >= HttpStatusCode.OK && statusCode <= (HttpStatusCode)299;
        }

        private bool UseSsl()
        {
            if (_clientConfig.BucketConfigs.ContainsKey(BucketName))
            {
                return _clientConfig.BucketConfigs[BucketName].UseSsl;
            }
            return _clientConfig.UseSsl;
        }

        private IResult WebRequestError(Exception ex, string message = "")
        {
            return new DefaultResult(false, string.IsNullOrEmpty(message) ? ex.Message : message, ex);
        }

        private string PostDataDicToString(IDictionary<string, object> postDataDictionary)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var key in postDataDictionary.Keys)
            {
                if (sb.Length > 0)
                {
                    sb.Append("&");
                }

                sb.Append(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(postDataDictionary[key].ToString()));
            }
            return sb.ToString();
        }
    }
}
