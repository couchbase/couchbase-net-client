using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Utils;

namespace Couchbase.Configuration
{
    internal class CouchbaseConfigContext : ConfigContextBase
    {
        public CouchbaseConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig)
            : base(bucketConfig, clientConfig)
        {
        }

        public CouchbaseConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig, 
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory) 
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory)
        {
        }

        public override void LoadConfig(IBucketConfig bucketConfig)
        {
            if (bucketConfig == null) throw new ArgumentNullException("bucketConfig");
            if (_bucketConfig == null || !_bucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes))
            {
                var nodes = bucketConfig.Nodes;
                for (var i = 0; i < nodes.Length; i++)
                {
                    var ip = bucketConfig.VBucketServerMap.ServerList[i];
                    var endpoint = GetEndPoint(ip, bucketConfig);
                    var connectionPool = _connectionPoolFactory(_clientConfig.PoolConfiguration, endpoint);
                    var ioStrategy = _ioStrategyFactory(connectionPool);
                    var server = new Core.Server(ioStrategy, nodes[i]);//this should be a Func factory...a functory
                    _servers.Add(server);
                }
            }
            if (_bucketConfig == null || !_bucketConfig.VBucketServerMap.Equals(bucketConfig.VBucketServerMap))
            {
                _keyMapper = new VBucketKeyMapper(_servers, bucketConfig.VBucketServerMap);
            }
            _bucketConfig = bucketConfig;
        }
    }
}
