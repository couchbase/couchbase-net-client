using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client.Providers;
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
        private bool _useSsl;
        private bool _useSslChanged;

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
        /// For synchronization with App.config or Web.configs.
        /// </summary>
        /// <param name="couchbaseClientSection"></param>
        internal ClientConfiguration(CouchbaseClientSection couchbaseClientSection)
        {
            UseSsl = couchbaseClientSection.UseSsl;
            SslPort = couchbaseClientSection.SslPort;
            ApiPort = couchbaseClientSection.ApiPort;
            DirectPort = couchbaseClientSection.DirectPort;
            MgmtPort = couchbaseClientSection.MgmtPort;
            HttpsMgmtPort = couchbaseClientSection.HttpsMgmtPort;
            HttpsApiPort = couchbaseClientSection.HttpsApiPort;
            Servers= new List<Uri>();
            foreach (var server in couchbaseClientSection.Servers)
            {
                Servers.Add(((UriElement)server).Uri);
            }
            foreach (var bucketElement in couchbaseClientSection.Buckets)
            {
                var bucket = (BucketElement) bucketElement;
                var bucketConfiguration = new BucketConfiguration
                {
                    BucketName = bucket.Name,
                    UseSsl = bucket.UseSsl,
                    Password = bucket.Password,
                    PoolConfiguration = new PoolConfiguration
                    {
                        MaxSize = bucket.ConnectionPool.MaxSize,
                        MinSize = bucket.ConnectionPool.MinSize,
                        WaitTimeout = bucket.ConnectionPool.WaitTimeout,
                        ShutdownTimeout = bucket.ConnectionPool.ShutdownTimeout,
                        UseSsl = bucket.ConnectionPool.UseSsl
                    }
                };
                BucketConfigs = new Dictionary<string, BucketConfiguration>();
                BucketConfigs.Add(bucket.Name, bucketConfiguration);
            }
        }

        /// <summary>
        /// Set to true to enable Secure Socket Layer (SSL) encryption of all traffic between the client and the server.
        /// </summary>
        public bool UseSsl
        {
            get { return _useSsl; }
            set
            {
                _useSsl = value;
                _useSslChanged = true;
            }
        }

        public int SslPort { get; set; }

        public int ApiPort { get; set; }

        public int MgmtPort { get; set; }

        public int DirectPort { get; set; }

        public int HttpsMgmtPort { get; set; }

        public int HttpsApiPort { get; set; }

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
            if (_serversChanged)
            {
                for(var i=0; i<_servers.Count(); i++)
                {
                    if (_servers[i].OriginalString.Contains("/pools")) { /*noop*/ }
                    else
                    {
                        var newUri = _servers[i].ToString();
                        newUri = string.Concat(newUri, "/pools");
                        _servers[i] = new Uri(newUri);
                    }
                }
            }
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
                if (_useSslChanged)
                {
                    for (var i = 0; i< _servers.Count(); i++)
                    {
                        if (UseSsl)
                        {
                            if (_servers[i].Port == (int)DefaultPorts.MgmtApi)
                            {
                                var oldUri = _servers[i];
                                var newUri = new Uri(string.Concat("https://", _servers[i].Host, 
                                    ":", (int)DefaultPorts.HttpsMgmt, oldUri.PathAndQuery));
                                _servers[i] = newUri;
                            }
                            foreach (var bucketConfig in BucketConfigs.Values)
                            {
                                bucketConfig.UseSsl = UseSsl;
                                bucketConfig.Port = (int)DefaultPorts.SslDirect;
                                bucketConfig.PoolConfiguration.UseSsl = UseSsl;
                            }
                        }
                    }
                }
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
