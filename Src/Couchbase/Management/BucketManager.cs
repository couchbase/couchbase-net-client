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
using System.Threading.Tasks;

namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Bucket.
    /// </summary>
    public sealed class BucketManager : IBucketManager
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
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