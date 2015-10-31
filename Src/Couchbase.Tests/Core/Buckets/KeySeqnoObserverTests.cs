using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.EnhancedDurability;
using Couchbase.IO.Strategies;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class KeySeqnoObserverTests
    {
        [Test]
        public void Test_KeySeqnoObserver()
        {

            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };

            var key = "Test_KeySeqnoObserver";
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    bucket.Remove(key);
                }
            }

            configuration.Initialize();

            var provider = new CarrierPublicationProvider(
                configuration,
                (pool) => new DefaultIOStrategy(pool),
                (config, endpoint) => new ConnectionPool<Connection>(config, endpoint),
                SaslFactory.GetFactory(),
                new DefaultConverter(),
                new DefaultTranscoder(new DefaultConverter(), new DefaultSerializer()));

            var configInfo = provider.GetConfig("default");

            var features = new List<short>();
            features.Add((short)ServerFeatures.MutationSeqno);

            var keyMapper = configInfo.GetKeyMapper();
            var mappedNode = keyMapper.MapKey(key);
            var node = mappedNode.LocatePrimary();

            foreach (var server in configInfo.Servers.Where(x=>x.IsDataNode))
            {
                var hello = new Hello("couchbase-net-sdk/2.1.4", features.ToArray(), provider.Transcoder, 0, 0);
                var result3 = server.Send(hello);
                Assert.IsTrue(result3.Success);
            }

            var result = node.Send(new Add<string>(key, "", (VBucket)mappedNode,
                new DefaultTranscoder(new DefaultConverter(), new DefaultSerializer()), 1000));

            var keyObserver = new KeySeqnoObserver(configInfo, 0, 1000);
            var durabilityReached = keyObserver.Observe(result.Token, ReplicateTo.Zero, PersistTo.One);
            Assert.IsTrue(durabilityReached);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
