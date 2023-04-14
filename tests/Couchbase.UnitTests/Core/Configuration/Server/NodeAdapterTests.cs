using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.UnitTests.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Server
{
    public class NodeAdapterTests
    {
        [Fact]
        public void Test_Config_Has_Query_But_HasQuery_Is_True()
        {
            var config = ResourceHelper.ReadResource("config-no-query-for-some-reason.json",
                InternalSerializationContext.Default.BucketConfig);

            var nodeAdapters = config.GetNodes();
            Assert.NotNull(nodeAdapters);
            Assert.True(nodeAdapters[2].IsQueryNode);
        }

        [Fact]
        public void Test_GlobalConfigWithAltAddresses()
        {
            var config = ResourceHelper.ReadResource("global-config-with-alt-addresses.json",
                InternalSerializationContext.Default.BucketConfig);

            var nodeAdapters = config.GetNodes();
            Assert.NotNull(nodeAdapters);
        }

        [Fact]
        public void IsAnalyticsNode_Is_True_When_Port_Is_Provided()
        {
            var node = new Node();
            var nodeExt = new NodesExt
            {
                Hostname = "localhost",
                Services = {Cbas = 8095}
            };

            var mockBucketConfig = new Mock<BucketConfig>();
            var adapter = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);

            Assert.Equal(8095, adapter.Analytics);
            Assert.True(adapter.IsAnalyticsNode);
        }

        [Fact]
        public void IsAnalyticsNodeSsl_Is_True_When_Port_Is_Provided()
        {
            var node = new Node();
            var nodeExt = new NodesExt
            {
                Hostname = "localhost",
                Services = {CbasSsl = 18095}
            };

            var mockBucketConfig = new Mock<BucketConfig>();
            var adapter = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);

            Assert.Equal(18095, adapter.AnalyticsSsl);
            Assert.True(adapter.IsAnalyticsNode);
        }

        [Fact]
        public void When_IPv6_NodeAdapter_Does_Not_Fail()
        {
            //arrange
            var serverConfig = ResourceHelper.ReadResource("config-with-ipv6.json",
                InternalSerializationContext.Default.BucketConfig);
            var mockBucketConfig = new Mock<BucketConfig>();

            //act
            var adapter = new NodeAdapter(serverConfig.Nodes[0], serverConfig.NodesExt[0], mockBucketConfig.Object);

            //assert
            Assert.NotNull(adapter);
        }

        [Theory]
        [InlineData("$HOST", "localhost")]
        [InlineData("$HOST:8091", "localhost")]
        [InlineData("192.168.1.1", "192.168.1.1")]
        [InlineData("192.168.1.1:8091", "192.168.1.1")]
        [InlineData("cb1.somewhere.org", "cb1.somewhere.org")]
        [InlineData("cb1.somewhere.org:8091", "cb1.somewhere.org")]
        [InlineData("::1", "::1")]
        [InlineData("[::1]", "[::1]")]
        [InlineData("[::1]:8091", "[::1]")]
        [InlineData("fd63:6f75:6368:2068", "fd63:6f75:6368:2068")]
        [InlineData("[fd63:6f75:6368:2068]", "[fd63:6f75:6368:2068]")]
        [InlineData("[fd63:6f75:6368:2068]:8091", "[fd63:6f75:6368:2068]")]
        [InlineData("fd63:6f75:6368:2068:1471:75ff:fe25:a8be", "fd63:6f75:6368:2068:1471:75ff:fe25:a8be")]
        [InlineData("[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]", "[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]")]
        [InlineData("[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]:8091", "[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]")]
        public void When_NodeExt_Hostname_Is_Null_NodeAdapter_Can_Parse_Hostname_and_Port_From_Node(string hostname, string expectedHostname)
        {
            var node = new Node
            {
                Hostname = hostname
            };
            var nodeExt = new NodesExt();
            var mockBucketConfig = new Mock<BucketConfig>();

            var adapter = new NodeAdapter(node, nodeExt, mockBucketConfig.Object);
            Assert.Equal(expectedHostname, adapter.Hostname);
        }

        [Fact]
        public void When_Node_is_null_Kv_service_should_Not_be_disabled()
        {
            const string hostname = "localhost";
            var nodeExt = new NodesExt
            {
                Hostname = hostname,
                Services = new Couchbase.Core.Configuration.Server.Services
                {
                    Kv = 11210 // nodeEXt has KV port, but node is null
                }
            };

            var adapter = new NodeAdapter(null, nodeExt, new BucketConfig());
            Assert.Equal(adapter.Hostname, hostname);

            Assert.True(adapter.IsKvNode);
        }

        [Theory]
        [InlineData(NetworkResolution.Auto, "external")]
        [InlineData(NetworkResolution.External, "external")]
        [InlineData(NetworkResolution.Default, "default")]
        public void When_NodeExt_Has_Alternate_Network_Configured_Use_External_Hostname(string networkType, string expected)
        {
            var node = new Node();
            var nodeExt = new NodesExt
            {
                Hostname = "default",
                AlternateAddresses = new Dictionary<string, ExternalAddressesConfig>
                {
                    {
                        "external", new ExternalAddressesConfig
                        {
                            Hostname = "external"
                        }
                    }
                }
            };

            var bucketConfig = new BucketConfig
            {
                NetworkResolution = networkType
            };

            var adapter = new NodeAdapter(node, nodeExt, bucketConfig);

            Assert.Equal(expected, adapter.Hostname);
        }

        [Theory]
        [InlineData(NetworkResolution.Auto, "external")]
        [InlineData(NetworkResolution.External, "external")]
        [InlineData(NetworkResolution.Default, "default")]
        public void When_NodeExt_Has_Alternate_Network_With_Ports_Configured_Use_External_Ports(string networkType, string expected)
        {
            var defaultServices = new Couchbase.Core.Configuration.Server.Services
            {
                Cbas = 1,
                CbasSsl = 2,
                Capi = 3,
                CapiSsl = 4,
                Fts = 5,
                FtsSsl = 6,
                Kv = 1,
                KvSsl = 2,
                N1Ql = 9,
                N1QlSsl = 10
            };
            var externalServices = new Couchbase.Core.Configuration.Server.Services
            {
                Cbas = 10,
                CbasSsl = 20,
                Capi = 30,
                CapiSsl = 40,
                Fts = 50,
                FtsSsl = 60,
                Kv = 10,
                KvSsl = 20,
                N1Ql = 90,
                N1QlSsl = 100
            };

            var node = new Node();
            var nodeExt = new NodesExt
            {
                Hostname = "default",
                Services = defaultServices,
                AlternateAddresses = new Dictionary<string, ExternalAddressesConfig>
                {
                    {
                        "external", new ExternalAddressesConfig
                        {
                            Hostname = "external",
                            Ports = externalServices
                        }
                    }
                }
            };

            var bucketConfig = new BucketConfig
            {
                NetworkResolution = networkType,
            };

            var adapter = new NodeAdapter(node, nodeExt, bucketConfig);
            VerifyServices(expected == "external" ? externalServices : defaultServices, adapter);
        }

        private void VerifyServices(Couchbase.Core.Configuration.Server.Services services, NodeAdapter adapter)
        {
            Assert.Equal(services.Cbas, adapter.Analytics);
            Assert.Equal(services.CbasSsl, adapter.AnalyticsSsl);
            Assert.Equal(services.Capi, adapter.Views);
            Assert.Equal(services.CapiSsl, adapter.ViewsSsl);
            Assert.Equal(services.Fts, adapter.Fts);
            Assert.Equal(services.FtsSsl, adapter.FtsSsl);
            Assert.Equal(services.Kv, adapter.KeyValue);
            Assert.Equal(services.KvSsl, adapter.KeyValueSsl);
            Assert.Equal(services.N1Ql, adapter.N1Ql);
            Assert.Equal(services.N1QlSsl, adapter.N1QlSsl);
        }
    }
}
