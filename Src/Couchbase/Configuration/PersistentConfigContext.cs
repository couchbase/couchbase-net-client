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
    internal class PersistentConfigContext : ConfigContextBase
    {
        public PersistentConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig)
            : base(bucketConfig, clientConfig)
        {
        }

        public PersistentConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig, 
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory) 
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory)
        {
        }

        //TODO need to make threadsafe, since multiple threads may(?) be calling...
        public override void LoadConfig(IBucketConfig bucketConfig)
        {
            if (bucketConfig == null) throw new ArgumentNullException("bucketConfig");
            if (_bucketConfig == null || !_bucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes))
            {
                foreach (var node in bucketConfig.VBucketServerMap.ServerList)
                {
                    var endpoint = GetEndPoint(node, bucketConfig);
                    var connectionPool = _connectionPoolFactory(_clientConfig.PoolConfiguration, endpoint);
                    var ioStrategy = _ioStrategyFactory(connectionPool);
                    var server = new Core.Server(ioStrategy);
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
