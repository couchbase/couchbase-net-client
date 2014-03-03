using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Core
{
    internal sealed class ClusterManager : IClusterManager
    {
        private readonly ClientConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, IBucket> _buckets = new ConcurrentDictionary<string, IBucket>();
        private readonly List<IConfigProvider> _configProviders = new List<IConfigProvider>();
        private Func<IBucketConfig> BucketConfigListener;
        private Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly bool _disposed;

        public ClusterManager(ClientConfiguration clientConfig)
            : this(clientConfig, 
            pool => new AwaitableIOStrategy(pool, null), 
            (config, endpoint) =>new DefaultConnectionPool(config, endpoint))
        {
            _clientConfig = clientConfig;
            Initialize();
        }

        public ClusterManager(ClientConfiguration clientConfig, Func<IConnectionPool, IOStrategy> ioStrategyFactory) 
            : this(clientConfig, 
            ioStrategyFactory, 
            (config, enpoint)=>new DefaultConnectionPool(config, enpoint))
        {
            _ioStrategyFactory = ioStrategyFactory;
            Initialize();
        }

        public ClusterManager(ClientConfiguration clientConfig, Func<IConnectionPool, IOStrategy> ioStrategyFactory, Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
            Initialize();
        }

        public List<IConfigProvider> ConfigProviders { get { return _configProviders; } }

        void Initialize()
        {
            foreach (var providerConfig in _clientConfig.ProviderConfigs )
            {
                var configProvider = CreateProvider(providerConfig);
                ConfigProviders.Add(configProvider);
                configProvider.Start();
            }
        }

        IConfigProvider CreateProvider(ProviderConfiguration providerConfiguration)
        {
            var type = Type.GetType(providerConfiguration.TypeName);
            return (IConfigProvider) Activator.CreateInstance(type, new object[] {_clientConfig});
        }

        public IConfigProvider GetProvider(string name)
        {
            throw new NotImplementedException();
        }

        public IBucket CreateBucket(string bucketName)
        {
            var configProvider = ConfigProviders.First();

            var bucket = new CouchbaseBucket(this, _clientConfig.PoolConfiguration, _ioStrategyFactory)
            {
                Name = bucketName
            };

            if (_buckets.TryAdd(bucketName, bucket))
            {
                configProvider.RegisterListener(bucket);
            }
            else
            {
                throw new BucketAlreadyOpenException(bucketName);
            }
            return bucket;
        }

        public IBucket CreateBucket(string bucketName, string username, string password)
        {
            throw new NotImplementedException();
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
            var provider = _configProviders.FirstOrDefault(x => x.GetType() == typeof (CarrierPublicationProvider));
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
            else
            {
                throw new BucketNotFoundException(bucket.Name);
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
            }
        }

        ~ClusterManager()
        {
            Dispose(false);
        }
    }
}
