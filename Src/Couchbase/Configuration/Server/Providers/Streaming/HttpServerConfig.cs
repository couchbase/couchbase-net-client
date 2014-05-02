using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    /// <summary>
    /// A configuration info class for supporting HTTP streaming provider.
    /// </summary>
    internal class HttpServerConfig : AuthenticatingWebClient, IServerConfig
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;

        public HttpServerConfig(ClientConfiguration clientConfig) 
            : base("default", string.Empty)
        {
            _clientConfig = clientConfig;
        }

        public HttpServerConfig(ClientConfiguration clientConfig, string username, string password) 
            : base(username, password)
        {
            _clientConfig = clientConfig;
        }

        public Uri BootstrapServer { get; protected internal set; }

        public Pools Pools { get; protected internal set; }

        public List<BucketConfig> Buckets { get; protected internal set; }

        public Bootstrap Bootstrap { get; protected internal set; }

        public List<BucketConfig> StreamingHttp { get; set; }

        public void Initialize()
        {
            var servers = _clientConfig.Servers.Shuffle().ToList();
            foreach (var server in servers)
            {
                if (DownloadConfigs(server))
                {
                    break;
                }
            }
        }

        bool DownloadConfigs(Uri server)
        {
            var success = false;
            try
            {
                Log.Info(m=>m("Bootstrapping from {0}", server));
                Bootstrap = DownLoadConfig<Bootstrap>(server);
                Pools = DownLoadConfig<Pools>(Bootstrap.GetPoolsUri(server));
                Buckets = DownLoadConfig<List<BucketConfig>>(Pools.GetBucketUri(server));
                WriteTerseUris(Buckets, Pools);
                BootstrapServer = server;
                success = true;
                Log.Info(m=>m("Bootstrapped from {0}", server));
            }
            catch (BootstrapException e)
            {
                Log.Error(e);
                throw;
            }
            catch (WebException e)
            {
                Log.Error(m=>m("Bootstrapping failed from {0}: {1}", server, e));
                if (e.Status != WebExceptionStatus.ProtocolError) return success;
                var response = e.Response as HttpWebResponse;
                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new AuthenticationException(BucketName, e);
                    }
                }
            }
            return success;
        }

        void WriteTerseUris(IEnumerable<BucketConfig> bucketConfigs, Pools pools)
        {
            var buckets = pools.Buckets;
            foreach (var bucketConfig in bucketConfigs)
            {
                bucketConfig.TerseUri = string.Concat(buckets.TerseBucketsBase, bucketConfig.Name);
                bucketConfig.TerseStreamingUri = string.Concat(buckets.TerseStreamingBucketsBase, bucketConfig.Name);
            }
        }

        T DownLoadConfig<T>(Uri uri)
        {
            var response = DownloadString(uri);
            return JsonConvert.DeserializeObject<T>(response);
        }
    }
}
