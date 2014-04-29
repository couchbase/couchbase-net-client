using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;

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
            pool => new SocketAsyncStrategy(pool, new PlainTextMechanism("default", string.Empty)),
            (config, endpoint) => new DefaultConnectionPool(config, endpoint))
        {
        }

        public ClusterManager(ClientConfiguration clientConfig, Func<IConnectionPool, IOStrategy> ioStrategyFactory)
            : this(clientConfig,
            ioStrategyFactory,
            (config, endpoint) => new DefaultConnectionPool(config, endpoint))
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
        private void Initialize()
        {
            _configProviders.Add(new CarrierPublicationProvider(_clientConfig, _ioStrategyFactory, _connectionPoolFactory));
            _configProviders.Add(new HttpStreamingProvider(_clientConfig, _ioStrategyFactory, _connectionPoolFactory));
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
                    var config = provider.GetConfig(bucketName, password);
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

                    var configObserver = bucket as IConfigObserver;
                    if (provider.RegisterObserver(configObserver) &&
                        _buckets.TryAdd(bucket.Name, bucket))
                    {
                        Log.DebugFormat("Successfully boostrap using {0}.", provider);
                        configObserver.NotifyConfigChanged(config);
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
                catch (AuthenticationException e)
                {
                    Log.Warn(e);
                    break;
                }
            }

            if (!success)
            {
                throw new ConfigException("Could not bootstrap {0}. See log for details.", bucketName);
            }
            return bucket;
        }

        private IServer CreateServer(Node node)
        {
            throw new NotImplementedException();
        }

        private IKeyMapper CreateMapper()
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
                var listener = temp as IConfigObserver;
                foreach (var configProvider in ConfigProviders)
                {
                    configProvider.UnRegisterObserver(listener);
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
