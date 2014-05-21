using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Serialization
{
    [TestFixture]
    public class BucketConfigTests
    {
        private BucketConfig _bucket1;
        private BucketConfig _bucket2;
        private BucketConfig _bucket3;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _bucket1 = new BucketConfig
            {
                Name = "default",
                AuthType = "SASL",
                NodeLocator = "vbucket",
                BucketType = "membase",
                SaslPassword = "",
                Uuid = "e3f109232cba04155b3182fd42cd1e83",
                VBucketServerMap = new VBucketServerMap
                {
                    HashAlgorithm = "CRC",
                    NumReplicas = 1,
                    ServerList = new[] {"192.168.56.101:11210", "192.168.56.104:11210"},
                    VBucketMap = new[] {new[] {1, 0}, new[] {1, 0}, new[] {1, 0}}
                },
                Nodes = new[]
                {
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.101:11210",
                        Status = "healthy"
                    },
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.104:11210",
                        Status = "healthy"
                    }
                }
            };

            _bucket2 = new BucketConfig
            {
                Name = "default",
                AuthType = "SASL",
                NodeLocator = "vbucket",
                BucketType = "membase",
                SaslPassword = "",
                Uuid = "e3f109232cba04155b3182fd42cd1e83",
                VBucketServerMap = new VBucketServerMap
                {
                    HashAlgorithm = "CRC",
                    NumReplicas = 1,
                    ServerList = new[] {"192.168.56.101:11210", "192.168.56.104:11210"},
                    VBucketMap = new[] {new[] {1, 0}, new[] {1, 0}, new[] {1, 0}}
                },
                Nodes = new[]
                {
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.101:11210",
                        Status = "healthy"
                    },
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.104:11210",
                        Status = "healthy"
                    }
                }
            };

            _bucket3 = new BucketConfig
            {
                Name = "test",
                AuthType = "SASL",
                NodeLocator = "vbucket",
                BucketType = "membase",
                SaslPassword = "",
                Uuid = "e3f109232cba04155b3182fd42cd1e83",
                VBucketServerMap = new VBucketServerMap
                {
                    HashAlgorithm = "CRC",
                    NumReplicas = 1,
                    ServerList = new[] {"192.168.56.101:11210", "192.168.56.104:11210"},
                    VBucketMap = new[] {new[] {1, 0}, new[] {1, 0}, new[] {1, 0}}
                },
                Nodes = new[]
                {
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.101:11210",
                        Status = "healthy"
                    },
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.104:11210",
                        Status = "healthy"
                    }
                }
            };
        }

        [Test]
        public void Test_GetHashcode()
        {
            Assert.AreEqual(_bucket1.GetHashCode(), _bucket2.GetHashCode());
            Assert.AreNotEqual(_bucket2.GetHashCode(), _bucket3.GetHashCode());
        }

        [Test]
        public void Test_Equals()
        {
            Assert.IsTrue(_bucket1.Equals(_bucket2));
            Assert.IsFalse(_bucket2.Equals(_bucket3));
        }

        [Test]
        public void Test_BucketConfig_Nodes()
        {
            var json = File.ReadAllText(@"Data\\Configuration\\terse-bucket-ssl.json");
            var bucket = JsonConvert.DeserializeObject<BucketConfig>(json);

            Assert.AreEqual(2, bucket.Nodes.Count());
            var node = bucket.Nodes.First();

            Assert.AreEqual("http://192.168.56.102:8092/default", node.CouchApiBase);
            Assert.AreEqual("192.168.56.102:8091", node.Hostname);
            Assert.AreEqual(11211, node.Ports.Proxy);
            Assert.AreEqual(11210, node.Ports.Direct);
            Assert.AreEqual(11207,node.Ports.SslDirect);
            Assert.AreEqual(18092,node.Ports.HttpsCapi);
            Assert.AreEqual(18091, node.Ports.HttpsMgmt);
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
