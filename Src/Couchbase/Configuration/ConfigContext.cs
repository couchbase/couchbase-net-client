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
    internal class ConfigContext : IConfigInfo
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private IBucketConfig _bucketConfig;
        private IKeyMapper _keyMapper;
        private readonly DateTime _creationTime;
        private readonly ClientConfiguration _clientConfig;
        private readonly List<IServer> _servers = new List<IServer>();
        private readonly Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private bool _disposed;

        public ConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig)
            : this(bucketConfig, clientConfig, pool => new AwaitableIOStrategy(pool, null),
                (config, endpoint) => new DefaultConnectionPool(config, endpoint))
        {
        }

        public ConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig, 
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
            _creationTime = DateTime.Now;
            LoadConfig(bucketConfig);
        }

        IPEndPoint GetEndPoint(string node, IBucketConfig bucketConfig)
        {
            const string blah = "$HOST";
            var hostName = node.Replace(blah, bucketConfig.SurrogateHost);
            return Core.Server.GetEndPoint(hostName);
        }

        //TODO need to make threadsafe, since multiple threads may(?) be calling...
        public void LoadConfig(IBucketConfig bucketConfig)
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
                switch (bucketConfig.NodeLocator.ToEnum<NodeLocatorEnum>())
                {
                    case NodeLocatorEnum.VBucket:
                        _keyMapper = new VBucketKeyMapper(_servers, bucketConfig.VBucketServerMap);
                        break;
                    case NodeLocatorEnum.Ketama:
                        _keyMapper = new KetamaKeyMapper(_servers);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            _bucketConfig = bucketConfig;
        }

        public DateTime CreationTime
        {
            get { return _creationTime; }
        }

        public IKeyMapper GetKeyMapper(string bucketName)
        {
            return _keyMapper;
        }

        public IBucketConfig BucketConfig
        {
            get { return _bucketConfig; }
        }

        public string BucketName
        {
            get { return _bucketConfig.Name; }
        }

        public ClientConfiguration ClientConfig
        {
            get { return _clientConfig; }
        }

        public BucketTypeEnum BucketType
        {
            get
            {
                BucketTypeEnum bucketType;
                if (!Enum.TryParse(_bucketConfig.BucketType, true, out bucketType))
                {
                    throw new NullConfigException("BucketType is not defined");
                }
                return bucketType;
            }
        }

        public NodeLocatorEnum NodeLocator
        {
            get
            {
                NodeLocatorEnum nodeLocator;
                if (!Enum.TryParse(_bucketConfig.NodeLocator, true, out nodeLocator))
                {
                    throw new NullConfigException("NodeLocator is not defined");
                }
                return nodeLocator;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed) return;
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
            _servers.ForEach(x=>x.Dispose());
            _disposed = false;
        }

        ~ConfigContext()
        {
            Dispose(false);
        }
    }
}
