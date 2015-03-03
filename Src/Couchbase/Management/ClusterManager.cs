using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Common.Logging;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Views;
using System.Threading.Tasks;
using Couchbase.Configuration.Server;


namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Cluster.
    /// </summary>
    public class ClusterManager : IClusterManager
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly IServerConfig _serverConfig;
        private readonly string _username;
        private readonly string _password;

        internal ClusterManager(ClientConfiguration clientConfig, IServerConfig serverConfig, HttpClient httpClient, IDataMapper mapper, string username, string password)
        {
            _clientConfig = clientConfig;
            _serverConfig = serverConfig;
            Mapper = mapper;
            HttpClient = httpClient;
            _password = password;
            _username = username;
        }


        public HttpClient HttpClient { get; set; }

        public IDataMapper Mapper { get; set; }

        /// <summary>
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult AddNode(string ipAddress)
        {
            IResult result;
            try
            {
                var uri = GetAPIUri("addNode");

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "POST";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);


                var formData = new Dictionary<string, object> { { "hostname", ipAddress }, { "user", _username }, { "password", _password } };
                var bytes = Encoding.UTF8.GetBytes(PostDataDicToString(formData));
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
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> AddNodeAsync(string ipAddress)
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
                        var uri = GetAPIUri("addNode");

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;

                        var request = new HttpRequestMessage(HttpMethod.Post, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                          Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            {"hostname", ipAddress},
                            {"user", _username},
                            {"password", _password}
                        });
                        request.Content.Headers.ContentType = contentType;

                        var task = client.PostAsync(uri, request.Content);
                        await task;

                        var message = task.Result;
                        var content = message.Content;
                        var stream = content.ReadAsStreamAsync();
                        await stream;

                        var response = GetString(stream.Result);
                        result = new DefaultResult(message.IsSuccessStatusCode, response, null);
                        Log.Debug(m => m("AddNode: {0}", response));
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
        /// Removes a failed over node from the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        /// <remarks>The node must have been failed over before removing or else this operation will fail.</remarks>
        public IResult RemoveNode(string ipAddress)
        {
            IResult result;
            try
            {
                var uri = GetAPIUri("ejectNode");

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "POST";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);

                var formData = new Dictionary<string, object>
                              {
                                 {"otpNode", string.Format("ns_1@{0}", ipAddress)},
                                 {"user", _username},
                                 {"password", _password}
                              };

                var bytes = Encoding.UTF8.GetBytes(PostDataDicToString(formData));
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
        /// Removes a failed over node from the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        /// <remarks>The node must have been failed over before removing or else this operation will fail.</remarks>
        public async Task<IResult> RemoveNodeAsync(string ipAddress)
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
                        var uri = GetAPIUri("ejectNode");

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;

                        var request = new HttpRequestMessage(HttpMethod.Post, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                          Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            {"otpNode", string.Format("ns_1@{0}", ipAddress)},
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

        /// <summary>
        /// Fails over a given node
        /// </summary>
        /// <param name="hostname">The name of the node to remove.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult FailoverNode(string hostname)
        {
            IResult result;
            try
            {
                var uri = GetAPIUri("failOver");

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "POST";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);

                var formData = new Dictionary<string, object>
                              {
                                   {"otpNode", string.Format("ns_1@{0}", hostname)},
                                   {"user", _username},
                                   {"password", _password}
                              };

                var bytes = Encoding.UTF8.GetBytes(PostDataDicToString(formData));
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
        /// Fails over a given node
        /// </summary>
        /// <param name="hostname">The name of the node to remove.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> FailoverNodeAsync(string hostname)
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
                        var uri = GetAPIUri("failOver");

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;

                        var request = new HttpRequestMessage(HttpMethod.Post, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                          Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            {"otpNode", string.Format("ns_1@{0}", hostname)},
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

        /// <summary>
        /// Initiates a rebalance across the cluster.
        /// </summary>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult Rebalance()
        {
            IResult result;
            try
            {
                var config = GetConfig(_username, _password);
                var knownNodes = config.Pools.Nodes.
                    Select(x => x.OtpNode);

                var ejectedNodes = config.Pools.Nodes.
                    Where(x => x.ClusterMembership.Equals("inactiveFailed")).
                    Select(x => x.OtpNode);

                var uri = GetAPIUri("rebalance");

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "POST";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);

                var formData = new Dictionary<string, object>
                              {
                                   {"ejectedNodes", string.Join(",", ejectedNodes)},
                                   {"knownNodes", string.Join(",", knownNodes)},
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
                        result = GetResult(response.StatusCode, reqStream);
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
        /// Initiates a rebalance across the cluster.
        /// </summary>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> RebalanceAsync()
        {
            IResult result;
            try
            {
                var config = GetConfig(_username, _password);
                var knownNodes = config.Pools.Nodes.
                    Select(x => x.OtpNode);

                var ejectedNodes = config.Pools.Nodes.
                    Where(x => x.ClusterMembership.Equals("inactiveFailed")).
                    Select(x => x.OtpNode);

                using (var handler = new HttpClientHandler
                {
                    Credentials = new NetworkCredential(_username, _password)
                })
                {
                    using (var client = new HttpClient(handler))
                    {
                        var uri = GetAPIUri("rebalance");

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;

                        var request = new HttpRequestMessage(HttpMethod.Post, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                          Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            {"ejectedNodes", string.Join(",", ejectedNodes)},
                            {"knownNodes", string.Join(",", knownNodes)},
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

        /// <summary>
        /// List all current buckets in this cluster.
        /// </summary>
        /// <returns>A list of buckets and their properties.</returns>
        public IResult<IList<BucketConfig>> ListBuckets()
        {
            IResult<IList<BucketConfig>> result;
            try
            {
                var config = GetConfig(_username, _password);
                result = new DefaultResult<IList<BucketConfig>>(true, "success", null)
                {
                    Value = config.Buckets
                };
            }
            catch (Exception e)
            {
                result = new DefaultResult<IList<BucketConfig>>(false, e.Message, e);
            }

            return result;
        }

        /// <summary>
        /// List all current buckets in this cluster.
        /// </summary>
        /// <returns>A list of buckets and their properties.</returns>
        public Task<IResult<IList<BucketConfig>>> ListBucketsAsync()
        {
            var result = this.ListBuckets();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="name">Required parameter. Name for new bucket.</param>
        /// <param name="ramQuota">The RAM quota in megabytes. The default is 100.</param>
        /// <param name="bucketType">Required parameter. Type of bucket to be created. “Memcached” configures as Memcached bucket. “Couchbase” configures as Couchbase bucket</param>
        /// <param name="replicaNumber">The number of replicas of each document: minimum 0, maximum 3.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="indexReplicas">Disable or enable indexes for bucket replicas.</param>
        /// <param name="flushEnabled">Enables the flush functionality on the specified bucket.</param>
        /// <param name="parallelDbAndViewCompaction">Indicates whether database and view files on disk can be compacted simultaneously.</param>
        /// <param name="saslPassword">Optional Parameter. String. Password for SASL authentication. Required if SASL authentication has been enabled.</param>
        /// <param name="threadNumber">Optional Parameter. Integer from 2 to 8. Change the number of concurrent readers and writers for the data bucket. </param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult CreateBucket(string name, uint ramQuota = 100, BucketTypeEnum bucketType = BucketTypeEnum.Couchbase, ReplicaNumber replicaNumber = ReplicaNumber.Two, AuthType authType = AuthType.Sasl, bool indexReplicas = false, bool flushEnabled = false, bool parallelDbAndViewCompaction = false, string saslPassword = "", ThreadNumber threadNumber = ThreadNumber.Two)
        {
            IResult result;
            try
            {
                var uri = GetBucketAPIUri();

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "POST";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);
                var formData = new Dictionary<string, object>
                              {
                                    {"user", _username},
                                    {"password", _password},
                                    {"name", name},
                                    {"authType", authType.ToString().ToLowerInvariant()},
                                    {"bucketType", bucketType.ToString().ToLowerInvariant()},
                                    {"flushEnabled", flushEnabled ? "0" : "1"},
                                    {"proxyPort", 0.ToString(CultureInfo.InvariantCulture)},
                                    {"parallelDBAndViewCompaction", parallelDbAndViewCompaction.ToString().ToLowerInvariant()},
                                    {"ramQuotaMB", ramQuota.ToString(CultureInfo.InvariantCulture)},
                                    {"replicaIndex", indexReplicas ? "0" : "1"},
                                    {"replicaNumber", ((int) replicaNumber).ToString(CultureInfo.InvariantCulture)},
                                    {"saslPassword", saslPassword},
                                    {"threadsNumber", ((int)threadNumber).ToString(CultureInfo.InvariantCulture)}
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
                        result = GetResult(response.StatusCode, reqStream);
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
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="name">Required parameter. Name for new bucket.</param>
        /// <param name="ramQuota">The RAM quota in megabytes. The default is 100.</param>
        /// <param name="bucketType">Required parameter. Type of bucket to be created. “Memcached” configures as Memcached bucket. “Couchbase” configures as Couchbase bucket</param>
        /// <param name="replicaNumber">The number of replicas of each document: minimum 0, maximum 3.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="indexReplicas">Disable or enable indexes for bucket replicas.</param>
        /// <param name="flushEnabled">Enables the flush functionality on the specified bucket.</param>
        /// <param name="parallelDbAndViewCompaction">Indicates whether database and view files on disk can be compacted simultaneously.</param>
        /// <param name="saslPassword">Optional Parameter. String. Password for SASL authentication. Required if SASL authentication has been enabled.</param>
        /// <param name="threadNumber">Optional Parameter. Integer from 2 to 8. Change the number of concurrent readers and writers for the data bucket. </param>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> CreateBucketAsync(string name, uint ramQuota = 100, BucketTypeEnum bucketType = BucketTypeEnum.Couchbase,
            ReplicaNumber replicaNumber = ReplicaNumber.Two, AuthType authType = AuthType.Sasl, bool indexReplicas = false, bool flushEnabled = false,
            bool parallelDbAndViewCompaction = false, string saslPassword = "", ThreadNumber threadNumber = ThreadNumber.Two)
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
                        var uri = GetBucketAPIUri();

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;

                        var request = new HttpRequestMessage(HttpMethod.Post, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                          Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            {"user", _username},
                            {"password", _password},
                            {"name", name},
                            {"authType", authType.ToString().ToLowerInvariant()},
                            {"bucketType", bucketType.ToString().ToLowerInvariant()},
                            {"flushEnabled", flushEnabled ? "0" : "1"},
                            {"proxyPort", 0.ToString(CultureInfo.InvariantCulture)},
                            {"parallelDBAndViewCompaction", parallelDbAndViewCompaction.ToString().ToLowerInvariant()},
                            {"ramQuotaMB", ramQuota.ToString(CultureInfo.InvariantCulture)},
                            {"replicaIndex", indexReplicas ? "0" : "1"},
                            {"replicaNumber", ((int) replicaNumber).ToString(CultureInfo.InvariantCulture)},
                            {"saslPassword", saslPassword},
                            {"threadsNumber", ((int)threadNumber).ToString(CultureInfo.InvariantCulture)}
                        });
                        request.Content.Headers.ContentType = contentType;

                        var task = client.PostAsync(uri, request.Content);

                        var postResult = await task;

                        result = await GetResult(postResult);
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
        /// Removes a bucket from the cluster permamently.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult RemoveBucket(string name)
        {
            IResult result;
            try
            {
                var uri = GetBucketAPIUri(name);

                var request = WebRequest.Create(uri) as HttpWebRequest;
                request.Method = "DELETE";
                request.Accept = request.ContentType = "application/x-www-form-urlencoded";
                request.Credentials = new NetworkCredential(_username, _password);

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
        /// Removes a bucket from the cluster permamently.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> RemoveBucketAsync(string name)
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
                        var uri = GetBucketAPIUri(name);

                        var contentType = new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
                        client.DefaultRequestHeaders.Accept.Add(contentType);
                        client.DefaultRequestHeaders.Host = uri.Authority;

                        var request = new HttpRequestMessage(HttpMethod.Delete, uri);
                        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Concat(_username, ":", _password))));

                        var task = client.DeleteAsync(uri);
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

        /// <summary>
        /// Returns the current state of the cluster.
        /// </summary>
        /// <returns></returns>
        public IResult<IClusterInfo> ClusterInfo()
        {
            IResult<IClusterInfo> result;
            try
            {
                var config = GetConfig(_username, _password);
                result = new DefaultResult<IClusterInfo>(true, "success", null)
                {
                    Value = new ClusterInfo(config)
                };
            }
            catch (Exception e)
            {
                result = new DefaultResult<IClusterInfo>(false, e.Message, e);
            }

            return result;
        }

        /// <summary>
        /// Returns the current state of the cluster.
        /// </summary>
        /// <returns></returns>
        public Task<IResult<IClusterInfo>> ClusterInfoAsync()
        {
            var result = this.ClusterInfo();
            return Task.FromResult(result);
        }

        HttpServerConfig GetConfig(string name, string password)
        {
            var config = new HttpServerConfig(_clientConfig, name, password);
            config.Initialize();
            return config;
        }

        static string GetString(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }

        async Task<IResult> GetResult(HttpResponseMessage httpResponseMessage)
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

        private IResult WebRequestError(Exception ex, string message = "")
        {
            return new DefaultResult(false, string.IsNullOrEmpty(message) ? ex.Message : message, ex);
        }

        Uri GetAPIUri(string apiKey)
        {
            var server = _clientConfig.Servers.First();
            var protocol = _clientConfig.UseSsl ? "https" : "http";
            var port = _clientConfig.UseSsl ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
            var api = _serverConfig.Pools.Controllers[apiKey].Uri;
            return new Uri(string.Format("{0}://{1}:{2}{3}", protocol, server.Host, port, api));
        }

        Uri GetBucketAPIUri(string bucketName=null)
        {
            var server = _clientConfig.Servers.First();
            var protocol = _clientConfig.UseSsl ? "https" : "http";
            var port = _clientConfig.UseSsl ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;

            var api = string.Format("{0}://{1}:{2}/pools/default/buckets", protocol, server.Host, port);
            if (!string.IsNullOrEmpty(bucketName))
            {
                api = string.Concat(api, "/", bucketName);
            }
            return new Uri(string.Format(api));
        }
    }
}
