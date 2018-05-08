using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Couchbase.Logging;
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
using Newtonsoft.Json;

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
            ThreadNumber threadNumber = ThreadNumber.Three)
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
            return CreateBucketAsync(settings.Name, settings.ProxyPort, settings.RamQuota, settings.BucketType, settings.ReplicaNumber,
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
        public Task<IResult> CreateBucketAsync(string name, uint ramQuota = 100,
            BucketTypeEnum bucketType = BucketTypeEnum.Couchbase,
            ReplicaNumber replicaNumber = ReplicaNumber.Two, AuthType authType = AuthType.Sasl,
            bool indexReplicas = false, bool flushEnabled = false,
            bool parallelDbAndViewCompaction = false, string saslPassword = "",
            ThreadNumber threadNumber = ThreadNumber.Three)
        {
            return CreateBucketAsync(name, 0, ramQuota, bucketType, replicaNumber, authType, indexReplicas,
                flushEnabled, parallelDbAndViewCompaction, saslPassword, threadNumber);
        }

        /// <summary>
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="name">Required parameter. Name for new bucket.</param>
        /// <param name="proxyPort">Optional parameter. Does not apply to Ephemeral buckets.</param>
        /// <param name="ramQuota">The RAM quota in megabytes. The default is 100.</param>
        /// <param name="bucketType">Required parameter. Type of bucket to be created. “Memcached” configures as Memcached bucket. “Couchbase” configures as Couchbase bucket</param>
        /// <param name="replicaNumber">The number of replicas of each document: minimum 0, maximum 3.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="indexReplicas">Disable or enable indexes for bucket replicas.</param>
        /// <param name="flushEnabled">Enables the flush functionality on the specified bucket.</param>
        /// <param name="parallelDbAndViewCompaction">Indicates whether database and view files on disk can be compacted simultaneously.</param>
        /// <param name="saslPassword">Optional Parameter. String. Password for SASL authentication. Required if SASL authentication has been enabled.</param>
        /// <param name="threadNumber">Optional Parameter. Integer from 2 to 8. Change the number of concurrent readers and writers for the data bucket.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        public IResult CreateBucket(string name, int proxyPort, uint ramQuota = 100,
            BucketTypeEnum bucketType = BucketTypeEnum.Couchbase, ReplicaNumber replicaNumber = ReplicaNumber.Two,
            AuthType authType = AuthType.Sasl, bool indexReplicas = false, bool flushEnabled = false,
            bool parallelDbAndViewCompaction = false, string saslPassword = "",
            ThreadNumber threadNumber = ThreadNumber.Three)
        {
            using (new SynchronizationContextExclusion())
            {
                return CreateBucketAsync(name, proxyPort, ramQuota, bucketType, replicaNumber, authType, indexReplicas,
                    flushEnabled, parallelDbAndViewCompaction, saslPassword, threadNumber).Result;
            }
        }

        /// <summary>
        /// Creates a new bucket on the cluster
        /// </summary>
        /// <param name="name">Required parameter. Name for new bucket.</param>
        /// <param name="proxyPort">Optional parameter. Does not apply to Ephemeral buckets.</param>
        /// <param name="ramQuota">The RAM quota in megabytes. The default is 100.</param>
        /// <param name="bucketType">Required parameter. Type of bucket to be created. “Memcached” configures as Memcached bucket. “Couchbase” configures as Couchbase bucket</param>
        /// <param name="replicaNumber">The number of replicas of each document: minimum 0, maximum 3.</param>
        /// <param name="authType">The authentication type.</param>
        /// <param name="indexReplicas">Disable or enable indexes for bucket replicas.</param>
        /// <param name="flushEnabled">Enables the flush functionality on the specified bucket.</param>
        /// <param name="parallelDbAndViewCompaction">Indicates whether database and view files on disk can be compacted simultaneously.</param>
        /// <param name="saslPassword">Optional Parameter. String. Password for SASL authentication. Required if SASL authentication has been enabled.</param>
        /// <param name="threadNumber">Optional Parameter. Integer from 2 to 8. Change the number of concurrent readers and writers for the data bucket.</param>
        /// <returns>
        /// A boolean value indicating the result.
        /// </returns>
        public async Task<IResult> CreateBucketAsync(string name, int proxyPort, uint ramQuota = 100,
            BucketTypeEnum bucketType = BucketTypeEnum.Couchbase, ReplicaNumber replicaNumber = ReplicaNumber.Two,
            AuthType authType = AuthType.Sasl, bool indexReplicas = false, bool flushEnabled = false,
            bool parallelDbAndViewCompaction = false, string saslPassword = "", ThreadNumber threadNumber = ThreadNumber.Three)
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
                {"parallelDBAndViewCompaction", parallelDbAndViewCompaction.ToString().ToLowerInvariant()},
                {"ramQuotaMB", ramQuota.ToString(CultureInfo.InvariantCulture)},
                {"replicaNumber", ((int) replicaNumber).ToString(CultureInfo.InvariantCulture)},
                {"saslPassword", saslPassword},
                {"threadsNumber", ((int) threadNumber).ToString(CultureInfo.InvariantCulture)}
            };

            if (bucketType != BucketTypeEnum.Ephemeral)
            {
                formData.Add("replicaIndex", indexReplicas ? "1" : "0");
                if (proxyPort > 0)
                {
                    formData.Add("proxyPort", proxyPort.ToString(CultureInfo.InvariantCulture));
                }
            }

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
            var config = new HttpServerConfig(_clientConfig, name, name, password);
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
            Log.Debug("{0}", body);
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

        #region UserManager

        private static async Task<string> GetResponseBody(HttpResponseMessage response)
        {
            if (response.Content != null)
            {
                return await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
            }

            return null;
        }

        /// <summary>
        /// Adds or replaces an existing Couchbase user with the provided <see cref="username" />, <see cref="password" />, <see cref="name" /> and <see cref="roles" />.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="name">The full name for the user.</param>
        /// <param name="roles">The list of roles for the user.</param>
        public IResult UpsertUser(AuthenticationDomain domain, string username, string password = null, string name = null, params Role[] roles)
        {
            using (new SynchronizationContextExclusion())
            {
                return UpsertUserAsync(domain, username, password, name, roles).Result;
            }
        }

        /// <summary>
        /// Asynchronously adds or replaces an existing Couchbase user with the provided <see cref="username" />, <see cref="password" />, <see cref="name" /> and <see cref="roles" />.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="name">The full name for the user.</param>
        /// <param name="roles">The roles.</param>
        public async Task<IResult> UpsertUserAsync(AuthenticationDomain domain, string username, string password = null, string name = null, params Role[] roles)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("username cannot be null or empty");
            }
            if (roles == null || !roles.Any())
            {
                throw new ArgumentException("roles cannot be null or empty");
            }

            switch (domain)
            {
                case AuthenticationDomain.Local:
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        throw new ArgumentException("password cannot be null or empty");
                    }
                    break;
                case AuthenticationDomain.External:
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        Log.Warn("Unable to update external user's password");
                    }
                    break;
            }

            var uri = GetUserManagementUri(domain, username);
            var formValues = GetUserFormValues(password, name, roles);
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                request.Content = new FormUrlEncodedContent(formValues);
                SetHeaders(request, uri);
                using (var response = await _httpClient.SendAsync(request).ContinueOnAnyContext())
                {
                    return new DefaultResult<bool>
                    {
                        Success = response.IsSuccessStatusCode,
                        Message = await GetResponseBody(response)
                    };
                }
            }
        }

        /// <summary>
        /// Removes a Couchbase user with the <see cref="username" />.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        public IResult RemoveUser(AuthenticationDomain domain, string username)
        {
            using (new SynchronizationContextExclusion())
            {
                return RemoveUserAsync(domain, username).Result;
            }
        }

        /// <summary>
        /// Asynchronously removes a Couchbase user with the <see cref="username" />.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        public async Task<IResult> RemoveUserAsync(AuthenticationDomain domain, string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("username cannot be null or empty");
            }

            var uri = GetUserManagementUri(domain, username);
            using (var request = new HttpRequestMessage(HttpMethod.Delete, uri))
            {
                SetHeaders(request, uri);
                using (var response = await _httpClient.SendAsync(request).ContinueOnAnyContext())
                {
                    return new DefaultResult<bool>
                    {
                        Success = response.IsSuccessStatusCode,
                        Message = await GetResponseBody(response)
                    };
                }
            }
        }

        /// <summary>
        /// Get a list of Couchbase users.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        public IResult<IEnumerable<User>> GetUsers(AuthenticationDomain domain)
        {
            using (new SynchronizationContextExclusion())
            {
                return GetUsersAsync(domain).Result;
            }
        }

        /// <summary>
        /// Asynchronously Get a list of Couchbase users.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        public async Task<IResult<IEnumerable<User>>> GetUsersAsync(AuthenticationDomain domain)
        {
            var uri = GetUserManagementUri(domain);
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                SetHeaders(request, uri);
                using (var response = await _httpClient.SendAsync(request).ContinueOnAnyContext())
                {
                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync().ContinueOnAnyContext())
                        {
                            return new DefaultResult<IEnumerable<User>>
                            {
                                Success = true,
                                Value = Mapper.Map<List<User>>(stream) ?? new List<User>()
                            };
                        }
                    }

                    return new DefaultResult<IEnumerable<User>>
                    {
                        Success = false
                    };
                }
            }
        }

        /// <summary>
        /// Get a Couchbase user using it's username.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        public IResult<User> GetUser(AuthenticationDomain domain, string username)
        {
            using (new SynchronizationContextExclusion())
            {
                return GetUserAsync(domain, username).Result;
            }
        }

        /// <summary>
        /// Asynchronously get a Couchbase user using it's username.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        public async Task<IResult<User>> GetUserAsync(AuthenticationDomain domain, string username)
        {
            var uri = GetUserManagementUri(domain, username);
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                SetHeaders(request, uri);
                using (var response = await _httpClient.SendAsync(request).ContinueOnAnyContext())
                {
                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = await response.Content.ReadAsStreamAsync().ContinueOnAnyContext())
                        {
                            return new DefaultResult<User>
                            {
                                Success = true,
                                Value = Mapper.Map<User>(stream) ?? new User()
                            };
                        }
                    }

                    return new DefaultResult<User>
                    {
                        Success = false
                    };
                }
            }
        }

        private Uri GetUserManagementUri(AuthenticationDomain domain, string username = null)
        {
            var scheme = _clientConfig.UseSsl ? "https" : "http";
            var host = _clientConfig.Servers.Shuffle().First().Host;
            var port = _clientConfig.UseSsl ? _clientConfig.HttpsMgmtPort : _clientConfig.MgmtPort;

            var userManagementPath = "settings/rbac/users/" + (domain == AuthenticationDomain.Local ? "local" : "external");
            var builder = new UriBuilder(scheme, host, port, userManagementPath);

            if (!string.IsNullOrWhiteSpace(username))
            {
                builder.Path = string.Format("{0}/{1}", builder.Path, username);
            }

            return builder.Uri;
        }

        private Uri BuildFtsManagementUri(string path)
        {
            var scheme = _clientConfig.UseSsl ? "https" : "http";
            var host = _clientConfig.Servers.Shuffle().First().Host;

            // TODO: read parts from config
            var port = _clientConfig.UseSsl ? 18094 : 8094;

            return new UriBuilder(scheme, host, port, path).Uri;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetUserFormValues(string password, string name, IEnumerable<Role> roles)
        {
            var rolesValue = string.Join(",",
                roles.Select(role => string.IsNullOrWhiteSpace(role.BucketName)
                    ? role.Name
                    : string.Format("{0}[{1}]", role.Name, role.BucketName))
            );

            var values = new Dictionary<string, string>
            {
                {"password", password},
                {"roles", rolesValue}
            };

            if (!string.IsNullOrWhiteSpace(name))
            {
                values.Add("name", name);
            }

            return values;
        }

        #endregion

        #region FTS Index Management

        internal const string SearchApiIndexPath = "/api/index";
        internal const string SearchApiStatsPath = "/api/stats";
        internal const string SearchApiPartitionPath = "/api/pindex";

        /// <summary>
        /// Gets all search index definitions asynchronously.
        /// </summary>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        public async Task<IResult<string>> GetAllSearchIndexDefinitionsAsync(CancellationToken token = default (CancellationToken))
        {
            var uri = BuildFtsManagementUri(SearchApiIndexPath);
            using (var response = await _httpClient.GetAsync(uri, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<string>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    var value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                    var json = JsonConvert.DeserializeObject<dynamic>(value);
                    result.Value = json.indexDefs.ToString(Formatting.None);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the search index definition asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        public async Task<IResult<string>> GetSearchIndexDefinitionAsync(string indexName, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            var path = Path.Combine(SearchApiIndexPath, indexName);
            var uri = BuildFtsManagementUri(path);
            using (var response = await _httpClient.GetAsync(uri, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<string>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    var value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                    var json = JsonConvert.DeserializeObject<dynamic>(value);
                    result.Value = json.indexDef.ToString(Formatting.None);
                }

                return result;
            }
        }

        /// <summary>
        /// Creates a search index asynchronously.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        public async Task<IResult<string>> CreateSearchIndexAsync(SearchIndexDefinition definition, CancellationToken token = default(CancellationToken))
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var path = Path.Combine(SearchApiIndexPath, definition.IndexName);
            var uri = BuildFtsManagementUri(path);
            using (var request = new HttpRequestMessage(HttpMethod.Put, uri))
            {
                var data = definition.ToJson();
                request.Content = new StringContent(data, Encoding.UTF8, MediaType.Json);
                using (var response = await _httpClient.SendAsync(request, token).ContinueOnAnyContext())
                {
                    // TODO: Should return new index UUID but server doesn't return it yet - leave it null for now
                    return new DefaultResult<string>
                    {
                        Success = response.IsSuccessStatusCode,
                        Message = response.ReasonPhrase,
                        Value = null
                    };
                }
            }
        }

        /// <summary>
        /// Deletes the search index asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        public async Task<IResult> DeleteSearchIndexAsync(string indexName, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            var path = Path.Combine(SearchApiIndexPath, indexName);
            var uri = BuildFtsManagementUri(path);
            using (var response = await _httpClient.DeleteAsync(uri, token).ContinueOnAnyContext())
            {
                return new DefaultResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };
            }
        }

        /// <summary>
        /// Gets the search index document count asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        public async Task<IResult<int>> GetSearchIndexDocumentCountAsync(string indexName, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            var path = Path.Combine(SearchApiIndexPath, indexName, "count");
            var uri = BuildFtsManagementUri(path);

            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            using (var response = await _httpClient.SendAsync(request, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<int>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    var value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                    var json = JsonConvert.DeserializeObject<dynamic>(value);
                    if (int.TryParse((string) json.count, out var count))
                    {
                        result.Value = count;
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// Sets the search index ingestion mode asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="ingestionMode">The ingestion mode.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental </remarks>
        public async Task<IResult> SetSearchIndexIngestionModeAsync(string indexName, SearchIndexIngestionMode ingestionMode, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            const string ingestionControlPath = "ingestControl";
            var path = Path.Combine(SearchApiIndexPath, indexName, ingestionControlPath, ingestionMode.GetDescription());
            var uri = BuildFtsManagementUri(path);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            using (var response = await _httpClient.SendAsync(request, token).ContinueOnAnyContext())
            {
                return new DefaultResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };
            }
        }

        /// <summary>
        /// Sets the search index query mode asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="queryMode">The query mode.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        public async Task<IResult> SetSearchIndexQueryModeAsync(string indexName, SearchIndexQueryMode queryMode, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            const string queryControlPath = "queryControl";
            var path = Path.Combine(SearchApiIndexPath, indexName, queryControlPath, queryMode.ToString().ToLowerInvariant());
            var uri = BuildFtsManagementUri(path);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            using (var response = await _httpClient.SendAsync(request, token).ContinueOnAnyContext())
            {
                return new DefaultResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };
            }
        }

        /// <summary>
        /// Sets the search index plan mode asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="planFreezeMode">The plan freeze mode.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        public async Task<IResult> SetSearchIndexPlanModeAsync(string indexName, SearchIndexPlanFreezeMode planFreezeMode, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            const string freezeControlPath = "planFreezeControl";
            var path = Path.Combine(SearchApiIndexPath, indexName, freezeControlPath, planFreezeMode.ToString().ToLowerInvariant());
            var uri = BuildFtsManagementUri(path);
            using (var request = new HttpRequestMessage(HttpMethod.Post, uri))
            using (var response = await _httpClient.SendAsync(request, token).ContinueOnAnyContext())
            {
                return new DefaultResult
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };
            }
        }

        /// <summary>
        /// Gets the search index statistics asynchronously.
        /// </summary>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        public async Task<IResult<string>> GetSearchIndexStatisticsAsync(CancellationToken token = default(CancellationToken))
        {
            var path = Path.Combine(SearchApiStatsPath);
            var uri = BuildFtsManagementUri(path);
            using (var response = await _httpClient.GetAsync(uri, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<string>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    result.Value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the search index statistics asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        public async Task<IResult<string>> GetSearchIndexStatisticsAsync(string indexName, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            var path = Path.Combine(SearchApiStatsPath, "index", indexName);
            var uri = BuildFtsManagementUri(path);
            using (var response = await _httpClient.GetAsync(uri, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<string>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    result.Value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                }

                return result;
            }
        }

        /// <summary>
        /// Gets all search index partition information asynchronously.
        /// </summary>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        public async Task<IResult<string>> GetAllSearchIndexPartitionInfoAsync(CancellationToken token = default(CancellationToken))
        {
            var path = Path.Combine(SearchApiPartitionPath);
            var uri = BuildFtsManagementUri(path);
            using (var response = await _httpClient.GetAsync(uri, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<string>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    var value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                    var json = JsonConvert.DeserializeObject<dynamic>(value);
                    result.Value = json.pindexes.ToString(Formatting.None);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the search index partition information asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        public async Task<IResult<string>> GetSearchIndexPartitionInfoAsync(string indexName, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            var path = Path.Combine(SearchApiPartitionPath, indexName);
            var uri = BuildFtsManagementUri(path);
            using (var response = await _httpClient.GetAsync(uri, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<string>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    result.Value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the search index partition document count asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="T:System.Threading.CancellationToken" /> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        public async Task<IResult<int>> GetSearchIndexPartitionDocumentCountAsync(string indexName, CancellationToken token = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(indexName))
            {
                throw new ArgumentException("Index name cannot be empty.", nameof(indexName));
            }

            var path = Path.Combine(SearchApiPartitionPath, indexName, "count");
            var uri = BuildFtsManagementUri(path);
            using (var response = await _httpClient.GetAsync(uri, token).ContinueOnAnyContext())
            {
                var result = new DefaultResult<int>
                {
                    Success = response.IsSuccessStatusCode,
                    Message = response.ReasonPhrase
                };

                if (response.IsSuccessStatusCode)
                {
                    var value = await response.Content.ReadAsStringAsync().ContinueOnAnyContext();
                    var json = JsonConvert.DeserializeObject<dynamic>(value);
                    if (int.TryParse((string) json.count, out var count))
                    {
                        result.Value = count;
                    }
                }

                return result;
            }
        }

        #endregion

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
