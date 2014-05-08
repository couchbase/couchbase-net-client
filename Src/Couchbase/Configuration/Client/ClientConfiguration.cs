using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.IO;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// Represents the configuration of a <see cref="CouchbaseCluster"/> object. The <see cref="CouchbaseCluster"/> object
    /// will use this class to construct it's internals.
    /// </summary>
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

        /// <summary>
        /// A list of hosts used to bootstrap from.
        /// </summary>
        public List<Uri> Servers
        {
            get { return _servers; }
            set
            {
                _servers = value;
                _serversChanged = true;
            }
        }

        /// <summary>
        /// A map of <see cref="BucketConfiguration"/>s and their names.
        /// </summary>
        public Dictionary<string, BucketConfiguration> BucketConfigs
        {
            get { return _bucketConfigurations; }
            set
            {
                _bucketConfigurations = value;
                _bucketConfigurationsChanged = true;
            }
        }

        /// <summary>
        /// The configuration used for creating the <see cref="IConnectionPool"/> for each <see cref="IBucket"/>.
        /// </summary>
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