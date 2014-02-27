using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    internal class HttpServerConfig : WebClient, IServerConfig
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;

        public HttpServerConfig(ClientConfiguration clientConfig)
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
