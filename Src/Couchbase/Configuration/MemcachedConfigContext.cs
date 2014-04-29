using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.Utils;

namespace Couchbase.Configuration
{
    internal class MemcachedConfigContext : ConfigContextBase
    {
        public MemcachedConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig) : 
            base(bucketConfig, clientConfig)
        {
        }

        public MemcachedConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig, 
            Func<IConnectionPool, IOStrategy> ioStrategyFactory, 
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory) 
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory)
        {
        }

        protected IPEndPoint GetEndPoint(Node node, IBucketConfig bucketConfig)
        {
            const string couchbasePort = "8091";
            const string blah = "$HOST";

            var address = node.Hostname.Replace(blah, bucketConfig.SurrogateHost);
            address = address.Replace(couchbasePort, node.Ports.Direct.ToString(CultureInfo.InvariantCulture));
            var endpoint = Core.Server.GetEndPoint(address);

            return endpoint;
        }

        public override void LoadConfig(IBucketConfig bucketConfig)
        {
            if (bucketConfig == null) throw new ArgumentNullException("bucketConfig");
            if (_bucketConfig == null || !_bucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes))
            {
                foreach (var node in bucketConfig.Nodes)
                {
                    var endpoint = GetEndPoint(node, bucketConfig);
                    var connectionPool = _connectionPoolFactory(_clientConfig.PoolConfiguration, endpoint);
                    var ioStrategy = _ioStrategyFactory(connectionPool);
                    var server = new Core.Server(ioStrategy, node);
           
                    _servers.Add(server); //todo make atomic
                    _keyMapper = new KetamaKeyMapper(_servers);//todo make atomic
                    _bucketConfig = bucketConfig;
                }
            }
        }
    }
}
