using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Core
{
    internal sealed class ClusterManager : IClusterManager
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ClientConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, IBucket> _buckets = new ConcurrentDictionary<string, IBucket>();
        private readonly List<IConfigProvider> _configProviders = new List<IConfigProvider>();
        private readonly Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private bool _disposed;

        public ClusterManager(ClientConfiguration clientConfig) 
            : this(clientConfig, 
            pool => new SocketAsyncStrategy(pool), 
            (config, endpoint) =>new DefaultConnectionPool(config, endpoint))
        {
        }

        public ClusterManager(ClientConfiguration clientConfig, Func<IConnectionPool, IOStrategy> ioStrategyFactory) 
            : this(clientConfig, 
            ioStrategyFactory, 
            (config, enpoint)=>new DefaultConnectionPool(config, enpoint))
        {
        }

        public ClusterManager(ClientConfiguration clientConfig, Func<IConnectionPool, IOStrategy> ioStrategyFactory, Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
            Initialize();
        }

        public List<IConfigProvider> ConfigProviders { get { return _configProviders; } }

        //TODO possibly make providers instantiation configurable...maybe. perhaps.
        void Initialize()
        {
            _configProviders.Add(new CarrierPublicationProvider(_clientConfig, _ioStrategyFactory, _connectionPoolFactory));
            _configProviders.Add(new HttpStreamingProvider(_clientConfig, _ioStrategyFactory, _connectionPoolFactory));
            _configProviders.ForEach(x=>x.Start());
        }

        public IConfigProvider GetProvider(string name)
        {
            throw new NotImplementedException();
        }

        public IBucket CreateBucket(string bucketName)
        {
            return CreateBucket(bucketName, string.Empty);
        }

        public IBucket CreateBucket(string bucketName, string password)
        {
            //note we probably want to treat this whole process as an aggregate and not log
            //until the process has completed with success or failure then logged along with
            //the sequence of events that occurred.
            var success = false;
            IBucket bucket = null;
            foreach (var provider in _configProviders)
            {
                try
                {
                    Log.DebugFormat("Trying to boostrap with {0}.", provider);
                    var config = provider.GetConfig(bucketName);
                    switch (config.NodeLocator)
                    {
                        case NodeLocatorEnum.VBucket:
                            bucket = new CouchbaseBucket(this, bucketName);
                            break;
                        case NodeLocatorEnum.Ketama:
                            bucket = new MemcachedBucket(this, bucketName);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    //Autenticate the connections
                    config.Authenticate(bucketName, password);

                    var listener = bucket as IConfigListener;
                    if (provider.RegisterListener(listener) &&
                        _buckets.TryAdd(bucket.Name, bucket))
                    {
                        Log.DebugFormat("Successfully boostrap using {0}.", provider);
                        listener.NotifyConfigChanged(config);
                        success = true;
                        break;
                    }
                }
                catch (BucketNotFoundException e)
                {
                    Log.Warn(e);
                }
                catch (ConfigException e)
                {
                    Log.Warn(e);
                }
            }

            if (!success)
            {
                throw new ConfigException("Could not bootstrap {0}. See log for details.", bucketName);
            }
            return bucket;
        }

        IServer CreateServer(Node node)
        {
            throw new NotImplementedException();
        }

        IKeyMapper CreateMapper()
        {
            throw new NotImplementedException();
        }

        public void NotifyConfigPublished(IBucketConfig bucketConfig)
        {
            var provider = _configProviders.FirstOrDefault(x => x is CarrierPublicationProvider);
            if (provider != null)
            {
                var carrierPublicationProvider = provider as CarrierPublicationProvider;
                if (carrierPublicationProvider != null)
                {
                    carrierPublicationProvider.UpdateConfig(bucketConfig);
                }
            }
        }

        public void DestroyBucket(IBucket bucket)
        {
            IBucket temp;
            if (_buckets.TryRemove(bucket.Name, out temp))
            {
                var listener = temp as IConfigListener;
                foreach (var configProvider in ConfigProviders)
                {
                    configProvider.UnRegisterListener(listener);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                foreach (var pair in _buckets)
                {
                   DestroyBucket(pair.Value);
                }
                _disposed = true;
            }
        }

        ~ClusterManager()
        {
            Dispose(false);
        }
    }
}
