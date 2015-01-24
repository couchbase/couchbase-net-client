using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Bucket.
    /// </summary>
    public sealed class BucketManager : IBucketManager
    {
        private readonly static ILog Log = LogManager.GetLogger<BucketManager>();
        private readonly IClusterController _clusterController;
        private readonly ClientConfiguration _clientConfig;
        private readonly string _username;
        private readonly string _password;

        internal BucketManager(string bucketName, ClientConfiguration clientConfig, IClusterController clusterController, HttpClient httpClient, IDataMapper mapper, string username, string password)
        {
            BucketName = bucketName;
            _clientConfig = clientConfig;
            _clusterController = clusterController;
            Mapper = mapper;
            HttpClient = httpClient;
            _password = password;
            _username = username;
        }

        public HttpClient HttpClient { get; private set; }

        public IDataMapper Mapper { get; private set; }

        /// <summary>
        /// The name of the Bucket.
        /// </summary>
        public string BucketName { get; private set; }

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
                        task.Wait();

                        result = GetResult(task.Result);
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
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A design document object.</returns>
        public IResult<string> GetDesignDocument(string designDocName)
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
                        task.Wait();

                        var taskResult = task.Result;
                        var content = taskResult.Content;
                        var stream = content.ReadAsStreamAsync();
                        stream.Wait();

                        result = GetResultAsString(task.Result);
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
                        task.Wait();

                        var taskResult = task.Result;
                        var content = taskResult.Content;
                        var stream = content.ReadAsStreamAsync();
                        stream.Wait();

                        result = GetResult(task.Result);
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
        public IResult<string> GetDesignDocuments(bool includeDevelopment = false)
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
                        task.Wait();

                        result = GetResultAsString(task.Result);
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
                        task.Wait();

                        result = GetResult(task.Result);
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

        private IResult<string> GetResultAsString(HttpResponseMessage httpResponseMessage)
        {
            var content = httpResponseMessage.Content;
            var stream = content.ReadAsStreamAsync();
            stream.Wait();

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

        private IResult GetResult(HttpResponseMessage httpResponseMessage)
        {
            var content = httpResponseMessage.Content;
            var stream = content.ReadAsStreamAsync();
            stream.Wait();

            var body = GetString(stream.Result);
            var result = new DefaultResult
            {
                Message = httpResponseMessage.IsSuccessStatusCode ? "success" : body,
                Success = httpResponseMessage.IsSuccessStatusCode,
            };
            Log.Debug(m => m("{0}", body));
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
    }
}