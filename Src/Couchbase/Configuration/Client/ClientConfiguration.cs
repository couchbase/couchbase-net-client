using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Configuration.Client
{
    public class ClientConfiguration
    {
        public ClientConfiguration(Uri server)
        {
            Servers = new List<Uri> {server};
        }

        public ClientConfiguration(params Uri[] servers)
        {
            //obsolete - remove
            Servers = servers.ToList();
        }

        public ClientConfiguration() :
            this(new Uri("http://localhost:8091/pools"))
        {
            ProviderConfigs = new List<ProviderConfiguration>
            {
                new ProviderConfiguration()
            };
            BucketConfigs = new List<BucketConfiguration> {new BucketConfiguration()};
            PoolConfiguration = new PoolConfiguration();
        }

        public ClientConfiguration(PoolConfiguration poolConfiguration) :
            this(new Uri("http://localhost:8091/pools"))
        {
            ProviderConfigs = new List<ProviderConfiguration>
            {
                new ProviderConfiguration()
            };
            BucketConfigs = new List<BucketConfiguration> 
            { 
                new BucketConfiguration
                {
                    PoolConfiguration = poolConfiguration
                } 
            };
            PoolConfiguration = poolConfiguration;
        }

        public string BootstrapPath { get; set; }

        public List<Uri> Servers { get; set; }

        public List<ProviderConfiguration> ProviderConfigs { get; set; }

        public List<BucketConfiguration> BucketConfigs { get; set; }

        public PoolConfiguration PoolConfiguration { get; set; }
    }
}