using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.Utils;

namespace Couchbase.Core.Buckets
{
    public class CouchbaseBucket : IBucket, IConfigListener, IConfigPublisher
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IClusterManager _clusterManager;
        private readonly List<IServer> _servers = new List<IServer>();
        private readonly PoolConfiguration _poolConfiguration;
        private IBucketConfig _bucketConfig;
        private IKeyMapper _keyMapper;
        private volatile bool _disposed;

        private static int _configChangedCount;
        private static int _serversChangedCount;
        private static int _bucketsChangedCount;
        private Func<IConnectionPool, IOStrategy> _ioStrategyFactory; 

        internal CouchbaseBucket(IClusterManager clusterManager, PoolConfiguration poolConfiguration)
        { 
            _clusterManager = clusterManager;
            _poolConfiguration = poolConfiguration;
        }

        internal CouchbaseBucket(IClusterManager clusterManager, PoolConfiguration poolConfiguration, Func<IConnectionPool, IOStrategy> ioStrategyFactory)
        {
            _clusterManager = clusterManager;
            _poolConfiguration = poolConfiguration;
            _ioStrategyFactory = ioStrategyFactory;
        }

        public string Name { get; set; }

        void IConfigListener.NotifyConfigChanged(IConfigInfo configInfo)
        {
            var bucketConfig = configInfo.BucketConfig;
            var count = Interlocked.Increment(ref _configChangedCount);
           
            if (_bucketConfig == null || !_bucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes))
            {
                Interlocked.Increment(ref _serversChangedCount);
                _servers.Clear();

                foreach (var node in bucketConfig.Nodes)
                {
                    var server = CreateServer(bucketConfig, node);
                    _servers.Add(server);
                }
            }

            if (_bucketConfig == null || !_bucketConfig.VBucketServerMap.Equals(bucketConfig.VBucketServerMap))
            {
                Interlocked.Increment(ref _bucketsChangedCount);

                var vBuckets = CreateVBuckets(bucketConfig.VBucketServerMap);
                _keyMapper = new KeyMapper(vBuckets);
            }

            Log.Info(m=>m("NotifyConfigChanged {0} called {1} times!", configInfo.BucketConfig.Name, count));
            Log.Info(m=>m("Recreating the server {0} times", _serversChangedCount));
            Log.Info(m=>m("Recreating the keymapper {0} times", _bucketsChangedCount));

            _bucketConfig = bucketConfig;
        }

        void IConfigListener.NotifyConfigChanged(IConfigInfo configInfo, IConnectionPool connectionPool)
        {
            var bucketConfig = configInfo.BucketConfig;
            if (_bucketConfig == null || !_bucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes))
            {
                foreach (var node in bucketConfig.VBucketServerMap.ServerList)
                {
                    var ipAddress = GetEndPoint(bucketConfig, node);
                    if (connectionPool.EndPoint.Equals(ipAddress))
                    {
                        var server = CreateServer(connectionPool);
                        _servers.Add(server);
                    }
                    else
                    {
                        var server = CreateServer(bucketConfig, node);
                        _servers.Add(server);
                    }
                }
            }

            if (_bucketConfig == null || !_bucketConfig.VBucketServerMap.Equals(bucketConfig.VBucketServerMap))
            {
                Interlocked.Increment(ref _bucketsChangedCount);

                var vBuckets = CreateVBuckets(bucketConfig.VBucketServerMap);
                _keyMapper = new KeyMapper(vBuckets);
            }
            _bucketConfig = bucketConfig;
        }

        Dictionary<int, IVBucket> CreateVBuckets(VBucketServerMap vBucketServerMap)
        {
            var vBuckets = new Dictionary<int, IVBucket>();
            var vBucketMap = vBucketServerMap.VBucketMap;
            for (var i = 0; i < vBucketMap.Length; i++)
            {
                var primary = vBucketMap[i][0];
                var replica = vBucketMap[i][1];
                vBuckets.Add(i, new VBucket(_servers, i, primary, replica));
            }
            return vBuckets;
        }

        IOStrategy CreateIOStrategy(IPEndPoint endPoint)
        {
            var connectionPool = new DefaultConnectionPool(_poolConfiguration, endPoint);
            var ioStrategy = CreateIOStrategy(connectionPool);

            return ioStrategy;
        }

        IOStrategy CreateIOStrategy(IConnectionPool connectionPool)
        {
            return _ioStrategyFactory(connectionPool);
        }

        IServer CreateServer(IBucketConfig bucketConfig, string host)
        {
            var endPoint = GetEndPoint(bucketConfig, host);
            var ioStrategy = CreateIOStrategy(endPoint);
            var server = new Server(ioStrategy);

            return server;
        }

        IServer CreateServer(IConnectionPool connectionPool)
        {
            var ioStrategy = CreateIOStrategy(connectionPool);
            var server = new Server(ioStrategy);

            return server;
        }

        static IPEndPoint GetEndPoint(IBucketConfig bucketConfig, string server)
        {
            const string blah = "$HOST";

            var hostName = server.Replace(blah, bucketConfig.SurrogateHost);
            return Server.GetEndPoint(hostName);
        }

        IServer CreateServer(IBucketConfig bucketConfig, Node node)
        {
            var endPoint = GetEndPoint(bucketConfig, node);
            var ioStrategy = CreateIOStrategy(endPoint);
            var server = new Server(ioStrategy);

            return server;
        }

        static IPEndPoint GetEndPoint(IBucketConfig bucketConfig, Node node)
        {
            const string blah = "$HOST";
            const string httpPort = "8091";

            var hostName = node.Hostname.Replace(blah, bucketConfig.SurrogateHost);
            hostName = hostName.Replace(httpPort, node.Ports.Direct.ToString(CultureInfo.InvariantCulture));

            return Server.GetEndPoint(hostName);
        }

        //TODO possible remove
        void IConfigPublisher.NotifyConfigPublished(IBucketConfig bucketConfig)
        {
            _clusterManager.NotifyConfigPublished(bucketConfig);
        }

        public IOperationResult<T> Insert<T>(string key, T value)
        {
            var vBucket = _keyMapper.MapKey(key);
            var server = vBucket.LocatePrimary();

            var operation = new SetOperation<T>(key, value, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                Log.Debug(m => m("Requires retry {0}", key));
            }

            return operationResult;
        }


        public IOperationResult<T> Get<T>(string key)
        {
            var vBucket = _keyMapper.MapKey(key);
            var server = vBucket.LocatePrimary();

            var operation = new GetOperation<T>(key, vBucket);
            var operationResult = server.Send(operation);

            if (CheckForConfigUpdates(operationResult))
            {
                Log.Debug(m=>m("Requires retry {0}", key));
            }

            return operationResult;
        }

        bool CheckForConfigUpdates<T>(IOperationResult<T> operationResult)
        {
            var requiresRetry = false;
            if (operationResult.Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                var bucketConfig = ((OperationResult<T>)operationResult).GetConfig();
                if (bucketConfig != null)
                {
                    _clusterManager.NotifyConfigPublished(bucketConfig);
                    requiresRetry = true;
                }
            }
            return requiresRetry;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _clusterManager.DestroyBucket(this);
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                _disposed = true;
            }
        }

        ~CouchbaseBucket()
        {
            Dispose(false);
        }
    }
}
