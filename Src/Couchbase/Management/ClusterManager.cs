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
using Couchbase.Utils;


namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Cluster.
    /// </summary>
    public class ClusterManager : IClusterManager
    {
        private static readonly ILog Log = LogManager.GetLogger<ClusterManager>();
        private readonly ClientConfiguration _clientConfig;
        private readonly IServerConfig _serverConfig;
        private readonly string _username;
        private readonly string _password;
        const string Localhost = "127.0.0.1";
        private readonly HttpClient _httpClient;

        static readonly List<CouchbaseService> Services = new List<CouchbaseService>
        {
            CouchbaseService.Index,
            CouchbaseService.KV,
            CouchbaseService.N1QL
        };

        internal ClusterManager(ClientConfiguration clientConfig, IServerConfig serverConfig,
            IDataMapper mapper, HttpClient httpClient, string username, string password)
        {
            _clientConfig = clientConfig;
            _serverConfig = serverConfig;
            Mapper = mapper;
            _password = password;
            _username = username;
            _httpClient = httpClient;
        }

        public IDataMapper Mapper { get; set; }

        /// <summary>
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult AddNode(string ipAddress)
        {
            using (new SynchronizationContextExclusion())
            {
                return AddNodeAsync(ipAddress).Result;
            }
        }

        public async Task<IResult> AddNodeAsync(string ipAddress, params CouchbaseService[] services)
        {
            var uri = GetAPIUri("addNode");

            var formData = new Dictionary<string, string>
            {
                {"hostname", ipAddress},
                {"user", _username},
                {"password", _password}
            };

            if (services != null && services.Any())
            {
                formData.Add("services", ToArray(services));
            }

            return await PostFormDataAsync(uri, formData).ContinueOnAnyContext();
        }


        /// <summary>
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public Task<IResult> AddNodeAsync(string ipAddress)
        {
            return AddNodeAsync(ipAddress, null);
        }

        /// <summary>
        /// Removes a failed over node from the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        /// <remarks>The node must have been failed over before removing or else this operation will fail.</remarks>
        public IResult RemoveNode(string ipAddress)
        {
            using (new SynchronizationContextExclusion())
            {
                return RemoveNodeAsync(ipAddress).Result;
            }
        }

        /// <summary>
        /// Removes a failed over node from the cluster.
        /// </summary>
        /// <param name="ipAddress">The IPAddress of the node.</param>
        /// <returns>A boolean value indicating the result.</returns>
        /// <remarks>The node must have been failed over before removing or else this operation will fail.</remarks>
        public async Task<IResult> RemoveNodeAsync(string ipAddress)
        {
            var uri = GetAPIUri("ejectNode");

            var formData = new Dictionary<string, string>
            {
                {"otpNode", string.Format("ns_1@{0}", ipAddress)},
                {"user", _username},
                {"password", _password}
            };

            return await PostFormDataAsync(uri, formData).ContinueOnAnyContext();
        }

        /// <summary>
        /// Fails over a given node
        /// </summary>
        /// <param name="hostname">The name of the node to remove.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult FailoverNode(string hostname)
        {
            using (new SynchronizationContextExclusion())
            {
                return FailoverNodeAsync(hostname).Result;
            }
        }

        /// <summary>
        /// Fails over a given node
        /// </summary>
        /// <param name="hostname">The name of the node to remove.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> FailoverNodeAsync(string hostname)
        {
            var uri = GetAPIUri("failOver");

            var formData = new Dictionary<string, string>
            {
                {"otpNode", string.Format("ns_1@{0}", hostname)},
                {"user", _username},
                {"password", _password}
            };

            return await PostFormDataAsync(uri, formData).ContinueOnAnyContext();
        }

        /// <summary>
        /// Initiates a rebalance across the cluster.
        /// </summary>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult Rebalance()
        {
            using (new SynchronizationContextExclusion())
            {
                return RebalanceAsync().Result;
            }
        }

        /// <summary>
        /// Initiates a rebalance across the cluster.
        /// </summary>
        /// <returns>A boolean value indicating the result.</returns>
        public async Task<IResult> RebalanceAsync()
        {
            var uri = GetAPIUri("rebalance");

            var config = GetConfig(_username, _password);
            var knownNodes = config.Pools.Nodes.
                Select(x => x.OtpNode);

            var ejectedNodes = config.Pools.Nodes.
                Where(x => x.ClusterMembership.Equals("inactiveFailed")).
                Select(x => x.OtpNode);

            var formData = new Dictionary<string, string>
            {
                {"ejectedNodes", string.Join(",", ejectedNodes)},
                {"knownNodes", string.Join(",", knownNodes)},
                {"user", _username},
                {"password", _password}
            };

            return await PostFormDataAsync(uri, formData).ContinueOnAnyContext();
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
        public IResult CreateBucket(string name, uint ramQuota = 100,
            BucketTypeEnum bucketType = BucketTypeEnum.Couchbase, ReplicaNumber replicaNumber = ReplicaNumber.Two,
            AuthType authType = AuthType.Sasl, bool indexReplicas = false, bool flushEnabled = false,
            bool parallelDbAndViewCompaction = false, string saslPassword = "",
            ThreadNumber threadNumber = ThreadNumber.Two)
        {
            using (new SynchronizationContextExclusion())
            {
                return
                    CreateBucketAsync(name, ramQuota, bucketType, replicaNumber, authType, indexReplicas, flushEnabled,
                        parallelDbAndViewCompaction, saslPassword, threadNumber).Result;
            }
        }

        /// <summary>
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="settings">The settings for the bucket.</param>
        /// <returns></returns>
        public Task<IResult> CreateBucketAsync(BucketSettings settings)
        {
            return CreateBucketAsync(settings.Name, settings.RamQuota, settings.BucketType, settings.ReplicaNumber,
                settings.AuthType, settings.IndexReplicas, settings.FlushEnabled, settings.ParallelDbAndViewCompaction,
                settings.SaslPassword, settings.ThreadNumber);
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
        public async Task<IResult> CreateBucketAsync(string name, uint ramQuota = 100,
            BucketTypeEnum bucketType = BucketTypeEnum.Couchbase,
            ReplicaNumber replicaNumber = ReplicaNumber.Two, AuthType authType = AuthType.Sasl,
            bool indexReplicas = false, bool flushEnabled = false,
            bool parallelDbAndViewCompaction = false, string saslPassword = "",
            ThreadNumber threadNumber = ThreadNumber.Two)
        {
            var uri = GetBucketAPIUri();

            var formData = new Dictionary<string, string>
            {
                {"user", _username},
                {"password", _password},
                {"name", name},
                {"authType", authType.ToString().ToLowerInvariant()},
                {"bucketType", bucketType.ToString().ToLowerInvariant()},
                {"flushEnabled", flushEnabled ? "1" : "0"},
                {"proxyPort", 0.ToString(CultureInfo.InvariantCulture)},
                {"parallelDBAndViewCompaction", parallelDbAndViewCompaction.ToString().ToLowerInvariant()},
                {"ramQuotaMB", ramQuota.ToString(CultureInfo.InvariantCulture)},
                {"replicaIndex", indexReplicas ? "1" : "0"},
                {"replicaNumber", ((int) replicaNumber).ToString(CultureInfo.InvariantCulture)},
                {"saslPassword", saslPassword},
                {"threadsNumber", ((int) threadNumber).ToString(CultureInfo.InvariantCulture)}
            };

            return await PostFormDataAsync(uri, formData).ContinueOnAnyContext();
        }

        /// <summary>
        /// Removes a bucket from the cluster permamently.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>A boolean value indicating the result.</returns>
        public IResult RemoveBucket(string name)
        {
            using (new SynchronizationContextExclusion())
            {
                return RemoveBucketAsync(name).Result;
            }
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
                var uri = GetBucketAPIUri(name);
                var task = _httpClient.DeleteAsync(uri);
                await task.ContinueOnAnyContext();
                result = await GetResult(task.Result).ContinueOnAnyContext();
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
            await stream.ContinueOnAnyContext();

            var body = GetString(stream.Result);
            var result = new DefaultResult
            {
                Message = httpResponseMessage.IsSuccessStatusCode ? "success" : body,
                Success = httpResponseMessage.IsSuccessStatusCode,
            };
            Log.Debug(m => m("{0}", body));
            return result;
        }

        Uri GetAPIUri(string apiKey)
        {
            var server = _clientConfig.Servers.First();
            var protocol = _clientConfig.UseSsl ? "https" : "http";
            var port = _clientConfig.UseSsl ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
            var api = _serverConfig.Pools.Controllers[apiKey].Uri;
            return new Uri(string.Format("{0}://{1}:{2}{3}", protocol, server.Host, port, api));
        }

        Uri GetBucketAPIUri(string bucketName = null)
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

        // ReSharper disable once InconsistentNaming
        Uri GetAPIUri(string hostName, string uriFormat)
        {
            var protocol = _clientConfig.UseSsl ? "https" : "http";
            var port = _clientConfig.UseSsl ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;
            return new Uri(string.Format(uriFormat, protocol, hostName, port));
        }

        protected internal virtual async Task<IResult> PostFormDataAsync(Uri uri, Dictionary<string, string> formData)
        {
            IResult result;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                SetHeaders(request, uri);

                var task = _httpClient.SendAsync(request);
                await task.ContinueOnAnyContext();
                result = await GetResult(task.Result).ContinueOnAnyContext();
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        static string ToArray(IList<CouchbaseService> services)
        {
            var type = typeof (CouchbaseService);
            var sb = new StringBuilder(services.Count());
            for (int i = 0; i < services.Count(); i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }
                // ReSharper disable once PossibleNullReferenceException
                sb.Append(Enum.GetName(type, services[i]).ToLower());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Initializes the entry point (EP) node of the cluster; similar to using the Management Console to setup a cluster.
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="path">The path to the data file.</param>
        /// <param name="indexPath">The index path to data file.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the operation.
        /// </returns>
        /// <remarks>
        /// See: <a href="http://docs.couchbase.com/admin/admin/Misc/admin-datafiles.html" />
        /// </remarks>
        public async Task<IResult> InitializeClusterAsync(string hostName = "127.0.0.1",
            string path = "/opt/couchbase/var/lib/couchbase/data",
            string indexPath = "/opt/couchbase/var/lib/couchbase/data")
        {
            IResult result;
            try
            {
                const string uriFormat = "{0}://{1}:{2}/nodes/self/controller/settings";
                var uri = GetAPIUri(hostName, uriFormat);

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"user", _username},
                        {"password", _password},
                        {"path", path},
                        {"index_path", indexPath},
                    })
                };
                SetHeaders(request, uri);

                var task = _httpClient.SendAsync(request);
                var postResult = await task.ContinueOnAnyContext();
                result = await GetResult(postResult).ContinueOnAnyContext();
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Renames the name of a node from it's default.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the operation.
        /// </returns>
        /// <remarks>In most cases this should just be the IP or hostname of node.</remarks>
        public async Task<IResult> RenameNodeAsync(string hostName = Localhost)
        {
            IResult result;
            try
            {
                const string uriFormat = "{0}://{1}:{2}/node/controller/rename";
                var uri = GetAPIUri(hostName, uriFormat);

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"user", _username},
                        {"password", _password},
                        {"hostname", hostName}
                    })
                };
                SetHeaders(request, uri);

                var task = _httpClient.SendAsync(request);
                var postResult = await task.ContinueOnAnyContext();
                result = await GetResult(postResult).ContinueOnAnyContext();
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Sets up the services that are available on a given node.
        /// </summary>
        /// <param name="hostName">The hostname or IP of the node.</param>
        /// <param name="services">The services - e.g. query, kv, and/or index</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the operation.
        /// </returns>
        public Task<IResult> SetupServicesAsync(string hostName, params CouchbaseService[] services)
        {
            return SetupServicesAsync(hostName, new List<CouchbaseService>(services));
        }

        /// <summary>
        /// Sets up the services that are available on a given node.
        /// </summary>
        /// <param name="hostName">The hostname or IP of the node.</param>
        /// <param name="services">The services - e.g. query, kv, and/or index</param>
        /// <returns>
        /// An <see cref="IResult" /> with the status of the operation.
        /// </returns>
        public async Task<IResult> SetupServicesAsync(string hostName = Localhost,
            List<CouchbaseService> services = null)
        {
            IResult result;
            try
            {
                const string uriFormat = "{0}://{1}:{2}/node/controller/setupServices";
                var uri = GetAPIUri(hostName, uriFormat);

                services = services ?? Services;
                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"user", _username},
                        {"password", _password},
                        {"services", ToArray(services)}
                    })
                };
                SetHeaders(request, uri);

                var task = _httpClient.SendAsync(request);
                var postResult = await task.ContinueOnAnyContext();
                result = await GetResult(postResult).ContinueOnAnyContext();
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Configures the memory asynchronous.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="memoryQuota">The memory quota.</param>
        /// <param name="indexMemQuota"></param>
        /// <returns></returns>
        public async Task<IResult> ConfigureMemoryAsync(string hostName, uint memoryQuota, uint indexMemQuota)
        {
            IResult result;
            try
            {
                const string uriFormat = "{0}://{1}:{2}/pools/default";
                var uri = GetAPIUri(hostName, uriFormat);

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"indexMemoryQuota", indexMemQuota.ToString()},
                        {"memoryQuota", memoryQuota.ToString()}
                    })
                };
                SetHeaders(request, uri);

                var task = _httpClient.SendAsync(request);
                var postResult = await task.ContinueOnAnyContext();
                result = await GetResult(postResult).ContinueOnAnyContext();
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Provisions the administartor account for an EP node.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <returns></returns>
        public async Task<IResult> ConfigureAdminAsync(string hostName)
        {
            IResult result;
            try
            {
                const string uriFormat = "{0}://{1}:{2}/settings/web";
                var uri = GetAPIUri(hostName, uriFormat);

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        {"password", _password},
                        {"username", _username},
                        {"port", "SAME"}
                    })
                };
                SetHeaders(request, uri);

                var task = _httpClient.SendAsync(request);
                var postResult = await task.ContinueOnAnyContext();
                result = await GetResult(postResult).ContinueOnAnyContext();

            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        /// <summary>
        /// Adds the sample bucket asynchronous.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="sampleBucketName">Name of the sample bucket.</param>
        /// <returns></returns>
        public async Task<IResult> AddSampleBucketAsync(string hostName, string sampleBucketName)
        {
            IResult result;
            try
            {
                const string uriFormat = "{0}://{1}:{2}/sampleBuckets/install";
                var uri = GetAPIUri(hostName, uriFormat);

                var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new StringContent("[\"" + sampleBucketName + "\"]")
                };
                SetHeaders(request, uri);

                var task = _httpClient.SendAsync(request);
                var postResult = await task.ContinueOnAnyContext();
                result = await GetResult(postResult).ContinueOnAnyContext();
            }
            catch (AggregateException e)
            {
                Log.Error(e);
                result = new DefaultResult(false, e.Message, e);
            }
            return result;
        }

        void SetHeaders(HttpRequestMessage request, Uri uri)
        {
            var contentType = new MediaTypeWithQualityHeaderValue(MediaType.Form);
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
