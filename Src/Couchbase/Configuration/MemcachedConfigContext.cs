using System;
using System.Globalization;
using System.Net;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Buckets;
using Couchbase.Core.Serializers;
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
            Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeSerializer serializer) 
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, converter, serializer)
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
        public override void LoadConfig(IBucketConfig bucketConfig)
        {
            if (bucketConfig == null) throw new ArgumentNullException("bucketConfig");
            if (BucketConfig == null || !BucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes))
            {
                foreach (var node in bucketConfig.Nodes)
                {
                    var endpoint = GetEndPoint(node, bucketConfig);
                    var connectionPool = ConnectionPoolFactory(ClientConfig.BucketConfigs[bucketConfig.Name].PoolConfiguration, endpoint);
                    var ioStrategy = IOStrategyFactory(connectionPool);
                    var server = new Core.Server(ioStrategy, node, ClientConfig);
                    var saslMechanism = SaslFactory(bucketConfig.Name, bucketConfig.Password, ioStrategy, Converter);
                    saslMechanism.IOStrategy = ioStrategy;
           
                    Servers.Add(server); //todo make atomic
                    KeyMapper = new KetamaKeyMapper(Servers);//todo make atomic
                    BucketConfig = bucketConfig;
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