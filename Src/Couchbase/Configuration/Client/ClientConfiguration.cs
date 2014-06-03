using System;
using System.Collections.Generic;
using System.Linq;
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
                        UseSsl = bucket.ConnectionPool.UseSsl, 
                    }
                };
                BucketConfigs = new Dictionary<string, BucketConfiguration> {{bucket.Name, bucketConfiguration}};
            }
        }

        /// <summary>
        /// Set to true to use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>Set to true to require all buckets to use SSL.</remarks>
        /// <remarks>Set to false and then set UseSSL at the individual Bucket level to use SSL on specific buckets.</remarks>
        public bool UseSsl
        {
            get { return _useSsl; }
            set
            {
                _useSsl = value;
                _useSslChanged = true;
            }
        }

        /// <summary>
        /// Overrides the default and sets the SSL port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 11207.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom SSL port.</remarks>
        public int SslPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Views REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Views REST API port.</remarks>
        public int ApiPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Management REST API port.</remarks>
        public int MgmtPort { get; set; }

            /// <summary>
            /// Overrides the default and sets the direct port to use for Key/Value operations using the Binary Memcached protocol.
            /// </summary>
            /// <remarks>The default and suggested direct port is 11210.</remarks>
            /// <remarks>Only set if you wish to override the default behavior.</remarks>
            /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom direct port.</remarks>
        public int DirectPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Management REST API SSL port.</remarks>
        public int HttpsMgmtPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Couchbase Views REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Views REST API SSL port.</remarks>
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
        public Dictionary<string, BucketConfiguration> BucketConfigs { get; set; }

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
                    if (_servers[i].OriginalString.EndsWith("/pools")) { /*noop*/ }
                    else
                    {
                        var newUri = _servers[i].ToString();
                        newUri = string.Concat(newUri, newUri.EndsWith("/") ? "pools" : "/pools");
                        _servers[i] = new Uri(newUri);
                    }
                }
            }

            //Update the bucket configs
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
                    bucketConfiguration.Servers = Servers.Select(x => x).ToList();
                }
                if (bucketConfiguration.Servers.Count == 0)
                {
                    bucketConfiguration.Servers.AddRange(Servers.Select(x=>x).ToList());
                }
                if (_useSslChanged)
                {
                    for (var i = 0; i < _servers.Count(); i++)
                    {
                        //Rewrite the URI's for boostrapping to use SSL.
                        if (UseSsl)
                        {
                            if (_servers[i].Port == MgmtPort)
                            {
                                var oldUri = _servers[i];
                                var newUri = new Uri(string.Concat("https://", _servers[i].Host, 
                                    ":", HttpsMgmtPort, oldUri.PathAndQuery));
                                _servers[i] = newUri;
                            }

                            //Setting ssl to true at parent level overrides child level ssl settings
                            foreach (var bucketConfig in BucketConfigs.Values)
                            {
                                bucketConfig.UseSsl = UseSsl;
                                bucketConfig.Port = SslPort;
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
