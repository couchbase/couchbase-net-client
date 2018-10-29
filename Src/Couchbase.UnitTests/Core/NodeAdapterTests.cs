using System.Linq;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core
{
    [TestFixture]
    public class NodeAdapterTests
    {
        [Test]
        public void IsAnalyticsNode_Is_True_When_Port_Is_Provided()
        {
            var node = new Node();
            var nodeExt = new NodeExt();
            nodeExt.Hostname = "localhost";
            nodeExt.Services.Analytics = 8095;

            var mockBucketConfig = new Mock<IBucketConfig>();
            var adapater = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);

            Assert.AreEqual(8095, adapater.Analytics);
            Assert.IsTrue(adapater.IsAnalyticsNode);
        }

        [Test]
        public void IsAnalyticsNodeSsl_Is_True_When_Port_Is_Provided()
        {
            var node = new Node();
            var nodeExt = new NodeExt();
            nodeExt.Hostname = "localhost";
            nodeExt.Services.AnalyticsSsl = 18095;

            var mockBucketConfig = new Mock<IBucketConfig>();
            var adapater = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);

            Assert.AreEqual(18095, adapater.AnalyticsSsl);
            Assert.IsTrue(adapater.IsAnalyticsNode);
        }

        [Test]
        public void When_IPv6_NodeAdapter_Does_Not_Fail()
        {
            //arrange
            var serverConfigJson = ResourceHelper.ReadResource("config_with_ipv6");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var mockBucketConfig = new Mock<IBucketConfig>();

            //act
            var adapter = new NodeAdapter(serverConfig.Nodes[0], serverConfig.NodesExt[0], mockBucketConfig.Object);

            //assert
            Assert.IsNotNull(adapter);
        }

        [Test]
        public void When_IPv6_NodeAdapter_GetEndpoint_Succeeds()
        {
            //arrange
            var serverConfigJson = ResourceHelper.ReadResource("config_with_ipv6");
            var serverConfig = JsonConvert.DeserializeObject<BucketConfig>(serverConfigJson);
            var mockBucketConfig = new Mock<IBucketConfig>();

            var adapter = new NodeAdapter(serverConfig.Nodes[0], serverConfig.NodesExt[0], mockBucketConfig.Object);

            //act
            var endpoint = adapter.GetIPEndPoint(false);

            //assert
            Assert.IsNotNull(endpoint);
        }

        [TestCase("$HOST", "localhost")]
        [TestCase("$HOST:8091", "localhost")]
        [TestCase("192.168.1.1", "192.168.1.1")]
        [TestCase("192.168.1.1:8091", "192.168.1.1")]
        [TestCase("cb1.somewhere.org", "cb1.somewhere.org")]
        [TestCase("cb1.somewhere.org:8091", "cb1.somewhere.org")]
        [TestCase("::1", "::1")]
        [TestCase("[::1]", "[::1]")]
        [TestCase("[::1]:8091", "[::1]")]
        [TestCase("fd63:6f75:6368:2068", "fd63:6f75:6368:2068")]
        [TestCase("[fd63:6f75:6368:2068]", "[fd63:6f75:6368:2068]")]
        [TestCase("[fd63:6f75:6368:2068]:8091", "[fd63:6f75:6368:2068]")]
        [TestCase("fd63:6f75:6368:2068:1471:75ff:fe25:a8be", "fd63:6f75:6368:2068:1471:75ff:fe25:a8be")]
        [TestCase("[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]", "[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]")]
        [TestCase("[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]:8091", "[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]")]
        public void When_NodeExt_Hostname_Is_Null_NodeAdapater_Can_Parse_Hostname_and_Port_From_Node(string hostname, string expectedHostname)
        {
            var node = new Node
            {
                Hostname = hostname
            };
            var nodeExt = new NodeExt();
            var mockBucketConfig = new Mock<IBucketConfig>();

            var adapter = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);
            Assert.AreEqual(expectedHostname, adapter.Hostname);
        }

        [Test]
        public void When_Node_is_null_Kv_service_should_be_disabled()
        {
            const string hostname = "localhost";
            var nodeExt = new NodeExt
            {
                Hostname = hostname,
                Services = new Services
                {
                    KV = 11210 // nodeEXt has KV port, but node is null
                }
            };

            var adapter = new NodeAdapter(null, nodeExt, null);
            Assert.AreEqual(adapter.Hostname, hostname);
            Assert.IsFalse(adapter.IsDataNode);
        }

        [TestCase(NetworkTypes.Auto, "external")]
        [TestCase(NetworkTypes.External, "external")]
        [TestCase(NetworkTypes.Default, "default")]
        [TestCase("", "default")]
        [TestCase(null, "default")]
        public void When_NodeExt_Has_Alternate_Network_Configured_Use_External_Hostname(string networkType, string expected)
        {
            var node = new Node();
            var nodeExt = new NodeExt
            {
                Hostname = "default",
                AlternateAddresses = new AlternateAddressesConfig
                {
                    External = new ExternalAddressesConfig
                    {
                        Hostname = "external"
                    }
                }
            };

            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.NetworkType).Returns(NetworkTypes.Auto);
            mockBucketConfig.Setup(x => x.SurrogateHost).Returns(expected);

            var adapter = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);

            Assert.AreEqual(expected, adapter.Hostname);
        }

        [TestCase(NetworkTypes.Auto, "external")]
        [TestCase(NetworkTypes.External, "external")]
        [TestCase(NetworkTypes.Default, "default")]
        [TestCase("", "default")]
        [TestCase(null, "default")]
        public void When_NodeExt_Has_Alternate_Network_With_Ports_Configured_Use_External_Ports(string networkType, string expected)
        {
            var defaultServices = new Services
            {
                Analytics = 1,
                AnalyticsSsl = 2,
                Capi = 3,
                CapiSSL = 4,
                Fts = 5,
                FtsSSL = 6,
                KV = 1,
                KvSSL = 2,
                N1QL = 9,
                N1QLSsl = 10
            };
            var externalServices = new Services
            {
                Analytics = 10,
                AnalyticsSsl = 20,
                Capi = 30,
                CapiSSL = 40,
                Fts = 50,
                FtsSSL = 60,
                KV = 10,
                KvSSL = 20,
                N1QL = 90,
                N1QLSsl = 100
            };

            var node = new Node();
            var nodeExt = new NodeExt
            {
                Hostname = "default",
                Services = defaultServices,
                AlternateAddresses = new AlternateAddressesConfig
                {
                    External = new ExternalAddressesConfig
                    {
                        Hostname = "external",
                        Ports = externalServices
                    }
                }
            };

            var mockBucketConfig = new Mock<IBucketConfig>();
            mockBucketConfig.Setup(x => x.NetworkType).Returns(NetworkTypes.Auto);
            mockBucketConfig.Setup(x => x.SurrogateHost).Returns(expected);

            var adapter = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);
            VerifyServices(expected == "external" ? externalServices : defaultServices, adapter);
        }

        private void VerifyServices(Services services, NodeAdapter adapter)
        {
            Assert.AreEqual(services.KV, adapter.KeyValue);
            Assert.AreEqual(services.KvSSL, adapter.KeyValueSsl);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
