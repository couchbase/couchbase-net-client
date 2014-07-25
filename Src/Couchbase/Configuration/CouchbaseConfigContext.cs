using System;
using System.Net;
using Common.Logging;
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
    /// Represents a configuration context for a Couchbase Bucket.
    /// </summary>
    internal sealed class CouchbaseConfigContext : ConfigContextBase
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();

        public CouchbaseConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeSerializer serializer) 
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, converter, serializer)
        {
        }

        /// <summary>
        /// Loads the most updated configuration creating any resources as needed.
        /// </summary>
        /// <param name="bucketConfig">The latest <see cref="IBucketConfig"/> 
        /// that will drive the recreation if the configuration context.</param>
        public override void LoadConfig(IBucketConfig bucketConfig)
        {
            if (bucketConfig == null) throw new ArgumentNullException("bucketConfig");
            if (BucketConfig == null || !BucketConfig.Nodes.AreEqual<Node>(bucketConfig.Nodes))
            {
                Log.Info(m=>m("Creating the Servers list using rev#{0}", bucketConfig.Rev));
                var nodes = bucketConfig.Nodes;
                for (var i = 0; i < nodes.Length; i++)
                {
                    var ip = bucketConfig.VBucketServerMap.ServerList[i];
                    var endpoint = GetEndPoint(ip, bucketConfig);
                    var connectionPool = ConnectionPoolFactory(ClientConfig.BucketConfigs[bucketConfig.Name].PoolConfiguration, endpoint);
                    var ioStrategy = IOStrategyFactory(connectionPool);
                    var saslMechanism = SaslFactory(bucketConfig.Name, bucketConfig.Password, ioStrategy, Converter);
                    ioStrategy.SaslMechanism = saslMechanism;
                    var server = new Core.Server(ioStrategy, nodes[i], ClientConfig);//this should be a Func factory...a functory
                    Servers.Add(server);
                }
            }
            if (BucketConfig == null || !BucketConfig.VBucketServerMap.Equals(bucketConfig.VBucketServerMap))
            {
                Log.Info(m => m("Creating the KeyMapper list using rev#{0}", bucketConfig.Rev));
                KeyMapper = new VBucketKeyMapper(Servers, bucketConfig.VBucketServerMap);
            }
            BucketConfig = bucketConfig;
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