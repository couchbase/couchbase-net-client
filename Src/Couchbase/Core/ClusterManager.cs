using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;

namespace Couchbase.Core
{
    internal class ClusterManager : IClusterManager
    {
        private readonly ClientConfiguration _clientConfig;
        private readonly ConcurrentDictionary<string, IBucket> _buckets = new ConcurrentDictionary<string, IBucket>();
        private readonly List<IConfigProvider> _configProviders = new List<IConfigProvider>();
        private Func<IBucketConfig> BucketConfigListener;

        public ClusterManager(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
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

            var bucket = new CouchbaseBucket(this, _clientConfig.PoolConfiguration)
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
    }
}
