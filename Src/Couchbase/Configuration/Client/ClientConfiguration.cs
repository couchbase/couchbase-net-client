using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Permissions;

namespace Couchbase.Configuration.Client
{
    public class ClientConfiguration
    {
        private const string DefaultBucket = "default";
        private readonly Uri _defaultServer = new Uri("http://localhost:8091/pools");
        private PoolConfiguration _poolConfiguration;
        private bool _poolConfigurationChanged;
        private List<Uri> _servers;
        private bool _serversChanged;
        private Dictionary<string, BucketConfiguration> _bucketConfigurations;
        private bool _bucketConfigurationsChanged;

        public ClientConfiguration()
        {
            PoolConfiguration = new PoolConfiguration();
            BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {DefaultBucket, new BucketConfiguration
                {
                    PoolConfiguration = PoolConfiguration
                }}
            };
            Servers = new List<Uri> { _defaultServer };

            //Set back to default
            _bucketConfigurationsChanged = false;
            _serversChanged = false;
            _poolConfigurationChanged = false;
        }

        public List<Uri> Servers
        {
            get { return _servers; }
            set
            {
                _servers = value;
                _serversChanged = true;
            }
        }

        public Dictionary<string, BucketConfiguration> BucketConfigs
        {
            get { return _bucketConfigurations; }
            set
            {
                _bucketConfigurations = value;
                _bucketConfigurationsChanged = true;
            }
        }

        public PoolConfiguration PoolConfiguration
        {
            get { return _poolConfiguration; }
            set
            {
                _poolConfiguration = value;
                _poolConfigurationChanged = true;
            }
        }

        internal void Initialize()
        {
            foreach (var keyValue in BucketConfigs)
            {
                var bucketConfiguration = keyValue.Value;
                if(string.IsNullOrEmpty(bucketConfiguration.BucketName))
                {
                    if (string.IsNullOrWhiteSpace(keyValue.Key))
                    {
                        throw new ArgumentException("bucketConfiguration.BucketName is null or empty.");
                    }
                    bucketConfiguration.BucketName = keyValue.Key;
                }
                if (bucketConfiguration.PoolConfiguration == null || _poolConfigurationChanged)
                {
                    bucketConfiguration.PoolConfiguration = PoolConfiguration;
                }
                if (bucketConfiguration.Servers == null || _serversChanged)
                {
                    bucketConfiguration.Servers = Servers.Select(x => x.Host).ToList();
                }
                if (bucketConfiguration.Servers.Count == 0)
                {
                    bucketConfiguration.Servers.AddRange(Servers.Select(x=>x.Host).ToList());
                }
            }
        }
    }
}