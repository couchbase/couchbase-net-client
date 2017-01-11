using Couchbase.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Management.Indexes;
using Couchbase.N1QL;
using Couchbase.Utils;

namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Bucket.
    /// </summary>
    public class BucketManager : IBucketManager
    {
        private static readonly ILog Log = LogManager.GetLogger<BucketManager>();
        private static readonly TimeSpan WatchIndexSleepDuration = TimeSpan.FromMilliseconds(50);

        private readonly ClientConfiguration _clientConfig;
        private readonly string _username;
        private readonly string _password;
        private readonly IBucket _bucket;
        private readonly HttpClient _httpClient;

        internal BucketManager(IBucket bucket, ClientConfiguration clientConfig, IDataMapper mapper, HttpClient httpClient, string username, string password)
        {
            _bucket = bucket;
            BucketName = bucket.Name;
            _clientConfig = clientConfig;
            Mapper = mapper;
            _password = password;
            _username = username;
            _httpClient = httpClient;
        }

        /// <summary>
        /// N1QL statements for creating and dropping indexes on the current bucket
        /// </summary>
        private static class Statements
        {
            public const string ListIndexes = "SELECT i.* FROM system:indexes AS i WHERE i.keyspace_id=\"{0}\" AND `using`=\"gsi\";";
            public const string CreatePrimaryIndex = "CREATE PRIMARY INDEX ON {0} USING GSI WITH {{\"defer_build\":{1}}};";
            public const string CreateNamedPrimaryIndex = "CREATE PRIMARY INDEX {0} ON {1} USING GSI WITH {{\"defer_build\":{2}}};";
            public const string DropPrimaryIndex = "DROP PRIMARY INDEX ON {0} USING GSI;";
            public const string DropNamedPrimaryIndex = "DROP INDEX {0}.{1} USING GSI;";
            public const string DropIndex = "DROP INDEX {0}.{1} USING GSI;";
            public const string CreateIndexWithFields = "CREATE INDEX {0} ON {1}({2}) USING GSI WITH {{\"defer_build\":{3}}};";
            public const string BuildIndexes = "BUILD INDEX ON {0}({1}) USING GSI;";
        }

        public IDataMapper Mapper { get; private set; }

        /// <summary>
        /// The name of the Bucket.
        /// </summary>
        public string BucketName { get; private set; }

        /// <summary>
        /// Lists the indexes for the current <see cref="IBucket" />.
        /// </summary>
        /// <returns></returns>
        public virtual IndexResult ListN1qlIndexes()
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
        public virtual async Task<IndexResult> ListN1qlIndexesAsync()
        {
            var request = new QueryRequest(string.Format(Statements.ListIndexes, BucketName));
            var result = await _bucket.QueryAsync<IndexInfo>(request).ContinueOnAnyContext();

            return new IndexResult
            {
                Value = result.Rows,
                Exception = result.Exception,
                Message = result.Message,
                Success = result.Success
            };
        }

        /// <summary>
        /// Watches all given indexes, polling the query service until they are "online" or the <param name="watchTimeout"/> has expired.
        /// </summary>
        /// <param name="indexNames">The list of indexes to watch for.</param>
        /// <param name="watchTimeout">The timeout for the watch.</param>
        public virtual IResult<List<IndexInfo>> WatchN1qlIndexes(List<string> indexNames, TimeSpan watchTimeout)
        {
            IndexResult result;
            var stopwatch = Stopwatch.StartNew();

            do
            {
                result = ListN1qlIndexes();
                if (!ShouldRetryWatch(result, indexNames))
                {
                    break;
                }

                Thread.Sleep(WatchIndexSleepDuration);
            } while (stopwatch.Elapsed < watchTimeout);

            return result;
        }

        /// <summary>
        /// Watches all given indexes, asynchronously polling the query service until they are "online" or the <param name="watchTimeout"/> has expired.
        /// </summary>
        /// <param name="indexNames">The list of indexes to watch for.</param>
        /// <param name="watchTimeout">The timeout for the watch.</param>
        public virtual async Task<IResult<List<IndexInfo>>> WatchN1qlIndexesAsync(List<string> indexNames, TimeSpan watchTimeout)
        {
            IndexResult result;
            var cancellationSource = new CancellationTokenSource(watchTimeout);

            do
            {
                result = await ListN1qlIndexesAsync();
                if (!ShouldRetryWatch(result, indexNames))
                {
                    break;
                }

                try
                {
                    await Task.Delay(WatchIndexSleepDuration, cancellationSource.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            } while (!cancellationSource.IsCancellationRequested);

            return result;
        }

        private static bool ShouldRetryWatch(IndexResult result, List<string> indexNames)
        {
            if (!result.Success) // Return on server returned error
            {
                return false;
            }

            var indexes = result.Value.Where(index => indexNames.Contains(index.Name)).ToList();
            if (!indexes.Any()) // Didn't recognise any index names to watch
            {
                return false;
            }

            foreach (var index in indexes)
            {
                Log.Debug("Index '{0}' is '{1}'", index.Name, index.State); // eg "Index 'foo' is 'online'"
            }

            return indexes.Any(index => index.State != "online");
        }

        /// <summary>
        /// Creates the primary index for the current bucket if it doesn't already exist.
        /// </summary>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        public virtual IResult CreateN1qlPrimaryIndex(bool defer = false)
        {
            var statement = string.Format(Statements.CreatePrimaryIndex,
                BucketName.N1QlEscape(), defer.ToString().ToLower());

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
        public virtual Task<IResult> CreateN1qlPrimaryIndexAsync(bool defer = false)
        {
            var statement = string.Format(Statements.CreatePrimaryIndex,
                BucketName.N1QlEscape(), defer.ToString().ToLower());

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
        public virtual Task<IResult> CreateN1qlPrimaryIndexAsync(string customName, bool defer = false)
        {
            var statement = string.Format(Statements.CreateNamedPrimaryIndex,
                customName.N1QlEscape(), BucketName.N1QlEscape(), defer.ToString().ToLower());

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
        public virtual Task<IResult> CreateN1qlIndexAsync(string indexName, bool defer = false, params string[] fields)
        {
            var fieldStr = string.Empty;
            if (fields != null)
            {
                fieldStr = fields.ToDelimitedN1QLString(',');
            }

            var statement = string.Format(Statements.CreateIndexWithFields,
               indexName.N1QlEscape(), BucketName.N1QlEscape(), fieldStr, defer.ToString().ToLower());

            var result = ExecuteIndexRequestAsync(statement);
            return result;
        }

        /// <summary>
        /// Drops the primary index of the current <see cref="IBucket" /> asynchronously.
        /// </summary>
        /// <returns>
        /// A <see cref="Task{IResult}" /> for awaiting on that contains the result of the method.
        /// </returns>
        public virtual Task<IResult> DropN1qlPrimaryIndexAsync()
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
        public virtual Task<IResult> DropNamedPrimaryIndexAsync(string customName)
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
        public virtual Task<IResult> DropN1qlIndexAsync(string name)
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
        public virtual Task<IResult[]> BuildN1qlDeferredIndexesAsync()
        {
            var tasks = new List<Task<IResult>>();
            var indexes = ListN1qlIndexes();

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
        /// Creates a primary index on the current <see cref="IBucket" /> reference.
        /// </summary>
        /// <param name="customName">The name of the index.</param>
        /// <param name="defer">If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        public virtual IResult CreateN1qlPrimaryIndex(string customName, bool defer = false)
        {
            var statement = string.Format(Statements.CreateNamedPrimaryIndex,
                customName.N1QlEscape(), BucketName.N1QlEscape(), defer.ToString().ToLower());

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
        public virtual IResult CreateN1qlIndex(string indexName, bool defer = false, params string[] fields)
        {
            var fieldStr = fields.ToDelimitedN1QLString(',');
            var statement = string.Format(Statements.CreateIndexWithFields,
                indexName.N1QlEscape(), BucketName.N1QlEscape(), fieldStr, defer.ToString().ToLower());

            var result = ExecuteIndexRequest(statement);
            return result;
        }

        /// <summary>
        /// Drops the primary index on the current <see cref="IBucket" />.
        /// </summary>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the request.
        /// </returns>
        public virtual IResult DropN1qlPrimaryIndex()
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
        public virtual IResult DropN1qlPrimaryIndex(string customName)
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
        public virtual IResult DropN1qlIndex(string name)
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
        public virtual IList<IResult> BuildN1qlDeferredIndexes()
        {
            var results = new List<IResult>();
            var indexes = ListN1qlIndexes();

            var deferredIndexes = indexes.Where(x => x.State == "pending" || x.State == "deferred").ToList();
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var index in deferredIndexes)
            {
                var statement = string.Format(Statements.BuildIndexes, BucketName.N1QlEscape(), index.Name.N1QlEscape());
                var result = ExecuteIndexRequest(statement);
                results.Add(result);
            }
            return results;
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
        public virtual IResult InsertDesignDocument(string designDocName, string designDoc)
        {
            using (new SynchronizationContextExclusion())
            {
                return InsertDesignDocumentAsync(designDocName, designDoc).Result;
            }
        }

        /// <summary>
        /// Inserts a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public virtual async Task<IResult> InsertDesignDocumentAsync(string designDocName, string designDoc)
        {
            IResult result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                var request = new HttpRequestMessage(HttpMethod.Put, uri)
                {
                    Content = new StringContent(designDoc)
                };
                SetHeaders(request, uri, MediaType.Json);

                var response = await _httpClient.SendAsync(request).ContinueOnAnyContext();
                result = await GetResult(response).ContinueOnAnyContext();
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
        public virtual IResult UpdateDesignDocument(string designDocName, string designDoc)
        {
            return InsertDesignDocument(designDocName, designDoc);
        }

        /// <summary>
        /// Updates a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public virtual Task<IResult> UpdateDesignDocumentAsync(string designDocName, string designDoc)
        {
            return InsertDesignDocumentAsync(designDocName, designDoc);
        }

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A design document object.</returns>
        public virtual IResult<string> GetDesignDocument(string designDocName)
        {
            using (new SynchronizationContextExclusion())
            {
                return GetDesignDocumentAsync(designDocName).Result;
            }
        }

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A design document object.</returns>
        public virtual async Task<IResult<string>> GetDesignDocumentAsync(string designDocName)
        {
            IResult<string> result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                SetHeaders(request, uri, MediaType.Form);

                var response = await _httpClient.SendAsync(request).ContinueOnAnyContext();
                result = await GetResultAsString(response).ContinueOnAnyContext();
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
        public virtual IResult RemoveDesignDocument(string designDocName)
        {
            using (new SynchronizationContextExclusion())
            {
                return RemoveDesignDocumentAsync(designDocName).Result;
            }
        }
        /// <summary>
        /// Removes a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public virtual async Task<IResult> RemoveDesignDocumentAsync(string designDocName)
        {
            IResult result;
            try
            {
                    var server = _clientConfig.Servers.First();
                    const string api = "{0}://{1}:{2}/{3}/_design/{4}";
                    var protocol = UseSsl() ? "https" : "http";
                    var port = UseSsl() ? _clientConfig.SslPort : _clientConfig.ApiPort;
                    var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName, designDocName));

                    var request = new HttpRequestMessage(HttpMethod.Delete, uri);
                    SetHeaders(request, uri, MediaType.Form);

                    var response = await _httpClient.SendAsync(request).ContinueOnAnyContext();
                    result = await GetResult(response).ContinueOnAnyContext();
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
        public virtual IResult<string> GetDesignDocuments(bool includeDevelopment = false)
        {
            using (new SynchronizationContextExclusion())
            {
                return GetDesignDocumentsAsync(includeDevelopment).Result;
            }
        }

        /// <summary>
        /// Lists all existing design documents.
        /// </summary>
        /// <param name="includeDevelopment">Whether or not to show development design documents in the results.</param>
        /// <returns>The design document as a string.</returns>
        public virtual async Task<IResult<string>> GetDesignDocumentsAsync(bool includeDevelopment = false)
        {
            IResult<string> result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/pools/default/buckets/{3}/ddocs";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName));

                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                SetHeaders(request, uri, MediaType.Json);

                var response = await _httpClient.SendAsync(request).ContinueOnAnyContext();
                result = await GetResultAsString(response).ContinueOnAnyContext();
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
            using (new SynchronizationContextExclusion())
            {
                return FlushAsync().Result;
            }
        }

        /// <summary>
        /// Destroys all documents stored within a bucket.  This functionality must also be enabled within the server-side bucket settings for safety reasons.
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating success.</returns>
        public virtual async Task<IResult> FlushAsync()
        {
            IResult result;
            try
            {
                var server = _clientConfig.Servers.First();
                const string api = "{0}://{1}:{2}/pools/default/buckets/{3}/controller/doFlush";
                var protocol = UseSsl() ? "https" : "http";
                var port = UseSsl() ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
                var uri = new Uri(string.Format(api, protocol, server.Host, port, BucketName));

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"user", _username},
                        {"password", _password}
                    })
                };
                SetHeaders(request, uri, MediaType.Form);

                var response = await _httpClient.SendAsync(request).ContinueOnAnyContext();
                result = await GetResult(response).ContinueOnAnyContext();
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        private async Task<IResult<string>> GetResultAsString(HttpResponseMessage httpResponseMessage)
        {
            var content = httpResponseMessage.Content;
            var body = await content.ReadAsStringAsync().ContinueOnAnyContext();

            var result = new DefaultResult<string>
            {
                Message = httpResponseMessage.IsSuccessStatusCode ? "success" : body,
                Success = httpResponseMessage.IsSuccessStatusCode,
                Value = httpResponseMessage.IsSuccessStatusCode ? body : null
            };
            Log.Debug("{0}", body);
            return result;
        }

        private async Task<IResult> GetResult(HttpResponseMessage httpResponseMessage)
        {
            var content = httpResponseMessage.Content;
            var body = await content.ReadAsStringAsync().ContinueOnAnyContext();

            var result = new DefaultResult
            {
                Message = httpResponseMessage.IsSuccessStatusCode ? "success" : body,
                Success = httpResponseMessage.IsSuccessStatusCode,
            };
            Log.Debug("{0}", body);
            return result;
        }

        private bool UseSsl()
        {
            if (_clientConfig.BucketConfigs.ContainsKey(BucketName))
            {
                return _clientConfig.BucketConfigs[BucketName].UseSsl;
            }
            return _clientConfig.UseSsl;
        }

        void SetHeaders(HttpRequestMessage request, Uri uri, string mediaType)
        {
            var contentType = new MediaTypeWithQualityHeaderValue(mediaType);
            if (request.Content != null)
            {
                request.Content.Headers.ContentType = contentType;
            }
            request.Headers.Accept.Add(contentType);
            request.Headers.Host = uri.Authority;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
