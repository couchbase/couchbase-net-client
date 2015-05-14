using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.Utils;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Represents a configuration context for a Memcached Bucket.
    /// </summary>
    internal class MemcachedConfigContext : ConfigContextBase
    {
        public MemcachedConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> saslFactory,
            ITypeTranscoder transcoder)
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, transcoder)
        {
        }

        /// <summary>
        /// Gets an <see cref="IPEndPoint"/> instance for a given Node and <see cref="IBucketConfig"/>
        /// </summary>
        /// <param name="node"></param>
        /// <param name="bucketConfig"></param>
        /// <returns></returns>
        protected IPEndPoint GetEndPoint(Node node, IBucketConfig bucketConfig)
        {
            const string couchbasePort = "8091";
            const string blah = "$HOST";

            var address = node.Hostname.Replace(blah, bucketConfig.SurrogateHost);
            address = address.Replace(couchbasePort, node.Ports.Direct.ToString(CultureInfo.InvariantCulture));
            return UriExtensions.GetEndPoint(address);
        }

        /// <summary>
        /// Loads the current configuration setting the internal state of this configuration context.
        /// </summary>
        /// <param name="bucketConfig"></param>
        /// <param name="force">True to force a reconfiguration.</param>
        public override void LoadConfig(IBucketConfig bucketConfig, bool force = false)
        {
            if (bucketConfig == null) throw new ArgumentNullException("bucketConfig");
            if (BucketConfig == null || !BucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes) || force)
            {
                var clientBucketConfig = ClientConfig.BucketConfigs[bucketConfig.Name];
                var servers = new Dictionary<IPAddress, IServer>();
                var nodes = BucketConfig.GetNodes();
                foreach (var adapter in nodes)
                {
                    var endpoint = IPEndPointExtensions.GetEndPoint(adapter, clientBucketConfig, BucketConfig);
                    try
                    {
                        var connectionPool = ConnectionPoolFactory(clientBucketConfig.PoolConfiguration, endpoint);
                        var ioStrategy = IOStrategyFactory(connectionPool);
                        var server = new Core.Server(ioStrategy, adapter, ClientConfig, bucketConfig, Transcoder)
                        {
                            SaslFactory = SaslFactory
                        };
                        server.CreateSaslMechanismIfNotExists();

                        servers.Add(endpoint.Address, server);
                    }
                    catch (Exception e)
                    {
                        Log.ErrorFormat("Could not add server {0}. Exception: {1}", endpoint, e);
                    }
                }
                var old = Interlocked.Exchange(ref Servers, servers);
                if (old != null)
                {
                    foreach (var server in old.Values)
                    {
                        server.Dispose();
                    }
                    old.Clear();
                }
            }
            Interlocked.Exchange(ref KeyMapper, new KetamaKeyMapper(Servers));
            Interlocked.Exchange(ref _bucketConfig, bucketConfig);
        }

        public override void LoadConfig()
        {
            var servers = new Dictionary<IPAddress, IServer>();
            var clientBucketConfig = ClientConfig.BucketConfigs[BucketConfig.Name];
            var nodes = BucketConfig.GetNodes();
            foreach (var adapter in nodes)
            {
                var endpoint = IPEndPointExtensions.GetEndPoint(adapter, clientBucketConfig, BucketConfig);
                try
                {
                    var connectionPool = ConnectionPoolFactory(clientBucketConfig.PoolConfiguration, endpoint);
                    var ioStrategy = IOStrategyFactory(connectionPool);

                    var server = new Core.Server(ioStrategy, adapter, ClientConfig, BucketConfig, Transcoder)
                    {
                        SaslFactory = SaslFactory
                    };
                    server.CreateSaslMechanismIfNotExists();

                    servers.Add(endpoint.Address, server);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Could not add server {0}. Exception: {1}", endpoint, e);
                }
            }
            var old = Interlocked.Exchange(ref Servers, servers);
            if (old != null)
            {
                foreach (var server in old.Values)
                {
                    server.Dispose();
                }
                old.Clear();
            }
            Interlocked.Exchange(ref KeyMapper, new KetamaKeyMapper(Servers));
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