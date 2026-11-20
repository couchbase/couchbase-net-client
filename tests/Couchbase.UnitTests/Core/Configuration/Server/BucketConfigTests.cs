using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Configuration.Server
{
    public class BucketConfigTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public BucketConfigTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void When_NetworkResolution_Is_External_Tls_Ports_Exist()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\private-link.json",
                InternalSerializationContext.Default.BucketConfig);
            config1.NetworkResolution = "external";

            Assert.Contains("11209", config1.VBucketServerMap.ServerList[0]);
            Assert.Contains("11210", config1.VBucketServerMap.ServerList[1]);
            Assert.Contains("11208", config1.VBucketServerMap.ServerList[2]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(512)]
        [InlineData(1023)]
        public void When_Rev_Same_And_VBucketMap_Different_Fail(int index)
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            var values = config2.VBucketServerMap.VBucketMap[index];
            config2.VBucketServerMap.VBucketMap[index] = new[] {values[1], values[0]};

            Assert.False(config2.Equals(config1));
        }


        [Fact]
        public void Test_Services_Are_Different()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\revision-28957.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\revision-28958.json",
                InternalSerializationContext.Default.BucketConfig);

            Assert.False(config2.Equals(config1));
            Assert.True(config1.HasClusterNodesChanged(config2));
        }

        [Fact]
        public void Test_Equals_True()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            Assert.True(config2.Equals(config1));
        }

        [Fact]
        public void Integration_Test_Environment_HasNodes()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-integration-tests.json",
                InternalSerializationContext.Default.BucketConfig);
            var nodes = config.GetNodes();
            Assert.Equal(2, nodes.Count);
        }

        [Fact]
        public void Test_Equals_False()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev96.json",
                InternalSerializationContext.Default.BucketConfig);

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_NodesExt_Changed()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            config2.NodesExt.RemoveAt(0);

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_Node_Changed()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            config2.Nodes.RemoveAt(0);

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_Node_Value_Changed()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            config2.Nodes[0].Hostname = "localhost:8091";

            Assert.False(config2.Equals(config1));
        }

       [Theory]
       [InlineData(true)]
       [InlineData(false)]
        public void Test_ClusterNodeChanged(bool hasChanged)
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            if (hasChanged)
            {
                config2.Nodes[0].Hostname = "localhost:8091";
                config2.Equals(config1);
                Assert.True(config1.HasClusterNodesChanged(config2));
            }
            else
            {
                config2.Equals(config1);
                Assert.False(config1.HasClusterNodesChanged(config2));
            }
        }

        [Fact]
        public void Test_Equals_NodeExt_Changed()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            config2.NodesExt.RemoveAt(0);

            Assert.False(config2.Equals(config1));
        }

        [Fact]
        public void Test_Equals_NodeExt_Value_Changed()
        {
            var config1 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);
            var config2 = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            config2.NodesExt[0].Hostname = "localhost:8091";

            Assert.False(config2.Equals(config1));
        }

        [Theory]
        [InlineData(@"Documents\Configs\rev92-single-node.json", @"Documents\Configs\rev94.json")]
        [InlineData(@"Documents\Configs\rev96.json", @"Documents\Configs\rev98-single-node.json")]
        public Task Test_Filter_Removed_Nodes(string oldConfigPath, string newConfigPath)
        {
            var oldConfig = ResourceHelper.ReadResource(oldConfigPath, InternalSerializationContext.Default.BucketConfig);
            var newConfig = ResourceHelper.ReadResource(newConfigPath, InternalSerializationContext.Default.BucketConfig);

            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            var bucketNodes = new ConcurrentDictionary<HostEndpointWithPort, IClusterNode>();
            var context = new ClusterContext(new CancellationTokenSource(), options);

            var ipEndpointService = context.ServiceProvider.GetRequiredService<IIpEndPointService>();

            //load up the initial state after bootstrapping
            foreach (var server in oldConfig.GetNodes())
            {
                var endPoint = HostEndpointWithPort.Create(server, options);
                var clusterNode = new ClusterNode(context, new Mock<IConnectionPoolFactory>().Object,
                    new Mock<ILogger<ClusterNode>>().Object,
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new Mock<ICircuitBreaker>().Object,
                    new Mock<ISaslMechanismFactory>().Object,
                    new Redactor(RedactionLevel.None),
                    endPoint,
                    server,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object)
                {
                    Owner = new FakeBucket("default", new ClusterOptions().WithPasswordAuthentication("username", "password"))
                };

                context.AddNode(clusterNode);
                bucketNodes.TryAdd(endPoint, clusterNode);
            }

            foreach (var nodesExt in newConfig.GetNodes())
            {
                var endPoint = HostEndpointWithPort.Create(nodesExt, options);
                if (bucketNodes.ContainsKey(endPoint))
                {
                    continue;
                }

                var clusterNode = new ClusterNode(context, new Mock<IConnectionPoolFactory>().Object,
                    new Mock<ILogger<ClusterNode>>().Object,
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new Mock<ICircuitBreaker>().Object, new Mock<ISaslMechanismFactory>().Object,
                    new Redactor(RedactionLevel.None), endPoint, nodesExt,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object);

                context.AddNode(clusterNode);
                bucketNodes.TryAdd(endPoint, clusterNode);
            }

            context.PruneNodes(newConfig);

            Assert.Equal(newConfig.NodesExt.Count, context.Nodes.Count);
            return Task.CompletedTask;
        }

        [Fact]
        public void When_NodesExt_Contains_Evicted_Node_It_Is_Removed()
        {
            //Arrange

            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-error.json",
                InternalSerializationContext.Default.BucketConfig);
            var originalExtCount = config.NodesExt.Count;
            //Act

            for (int i = 0; i < 10; i++)
            {
                var nodes = config.GetNodes();

                //Assert

                Assert.Equal(3, nodes.Count);
                Assert.Equal(originalExtCount, config.NodesExt.Count);
            }
        }

        [Fact]
        public void When_Config_Has_AlternateAddresses_Hostname_And_Ports_Are_Populated()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-alternate-addresses.json",
                InternalSerializationContext.Default.BucketConfig);
            config.NetworkResolution = NetworkResolution.Auto;

            Assert.Equal(3, config.Nodes.Count);
            Assert.Equal(3, config.NodesExt.Count);

            foreach (var nodeExt in config.NodesExt)
            {
                var alternateAddress = nodeExt.AlternateAddresses[NetworkResolution.External];
                Assert.NotEmpty(alternateAddress.Hostname);
                Assert.NotNull(alternateAddress.Ports);
            }
        }

        [Fact]
        public void When_Config_Has_AlternateAddresses_NodeAdapter_Uses_External_Hostname()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-alternate-addresses2.json",
                InternalSerializationContext.Default.BucketConfig);

            var nodes = config.GetNodes();
            var nodesExt = config.NodesExt;

            Assert.Empty(config.Nodes);
            Assert.Equal(3, config.NodesExt.Count);

            for (var i = 0; i < nodes.Count; i++ )
            {
                var alternateAddress = nodesExt[i].AlternateAddresses[NetworkResolution.External];
                Assert.Equal(nodes[i].Hostname, alternateAddress.Hostname);
            }
        }

        [Theory]
        [InlineData(NetworkResolution.External, 32178, 31903)]
        [InlineData(NetworkResolution.Default, 8093, 18093)]
        [InlineData(NetworkResolution.Auto, 32178, 31903)]
        public void Test_NetworkResolution(string networkResolution, int n1qlPort, int n1qlSslPort)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\missing-query.json",
                InternalSerializationContext.Default.BucketConfig);
            config.NetworkResolution = networkResolution;

            var nodes = config.GetNodes();
            var query = nodes.FirstOrDefault(x => x.IsQueryNode);

            Assert.NotNull(query);
            Assert.Equal(n1qlPort, query.N1Ql);
            Assert.Equal(n1qlSslPort, query.N1QlSsl);
        }

        [Fact]
        public void Test_MissingManagementNode()
        {
            var clusterOptions = new ClusterOptions().WithConnectionString("couchbase://UNIT_TEST_NO_SUCH_HOST").WithPasswordAuthentication("username", "password");
            var config = ResourceHelper.ReadResource(@"Documents\Configs\missing-management.json",
                InternalSerializationContext.Default.BucketConfig);
            var clusterContext = new ClusterContext(new CancellationTokenSource(), new ClusterOptions().WithPasswordAuthentication("username", "password"));
            clusterContext.GlobalConfig = config;
            Assert.Contains(config.NodesExt, n => n.Services.Mgmt == 0
                                                  && n.Services.MgmtSsl == 0);

            foreach (var nodeExt in config.NodesExt)
            {
                var nodeAdapter = new NodeAdapter(null, nodeExt, config);
                var mockClusterNode = new Mock<IClusterNode>(MockBehavior.Strict);
                mockClusterNode.SetupGet(x => x.NodesAdapter).Returns(nodeAdapter);
                mockClusterNode.SetupGet(x => x.ManagementUri).Returns(nodeAdapter.GetManagementUri(clusterOptions));
                clusterContext.Nodes.Add(mockClusterNode.Object);
            }

            Assert.Contains(clusterContext.Nodes, n => n.ManagementUri is null);

            var serviceUriProvider = new ServiceUriProvider(clusterContext);
            var managementNode = clusterContext.GetRandomNodeForService(ServiceType.Management);
            Assert.NotNull(managementNode);

            // Call GetRandomManagementUri() many times to ensure that the node with missing management Uri is not hit.
            for (int i = 0; i < 1_000; i++)
            {
                Assert.NotNull(serviceUriProvider.GetRandomManagementUri());
            }
        }

        [Theory]
        [InlineData(NetworkResolution.Auto, NetworkResolution.External, @"Documents\Configs\missing-query.json")]
        [InlineData(NetworkResolution.External, NetworkResolution.External, @"Documents\Configs\missing-query.json")]
        [InlineData(NetworkResolution.Auto, NetworkResolution.Default, @"Documents\Configs\config-integration-tests.json")]
        public void Test_SetNetworkResolution(string networkResolution, string effectiveResolution, string configPath)
        {
            var options = new ClusterOptions {NetworkResolution = networkResolution};

            var config = ResourceHelper.ReadResource(configPath, InternalSerializationContext.Default.BucketConfig);
            config.SetEffectiveNetworkResolution(options);

            Assert.Equal(effectiveResolution, options.EffectiveNetworkResolution);
        }

        [Fact]
        public void Test_Max_Revision_Size()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-bigly-yuge-rev.json",
                InternalSerializationContext.Default.BucketConfig);

            var expected = 18446744073709551615ul;
            Assert.Equal(expected, config.Rev);
        }

        [Theory]
        [InlineData("config_higher_rev_higher_epoch.json", 2ul, 2ul)]
        [InlineData("config_higher_rev_lower_epoch.json", 2ul, 1ul)]
        public void Test_RevEpoch(string configResource, ulong rev, ulong revEpoch)
        {
            var config = ResourceHelper.ReadResource(configResource, InternalSerializationContext.Default.BucketConfig);
            Assert.Equal(rev, config.Rev);
            Assert.Equal(revEpoch, config.RevEpoch);
        }

        [Fact]
        public void Test_Higher_Rev_Higher_Epoch_Config()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config_higher_rev_higher_epoch.json",
                InternalSerializationContext.Default.BucketConfig);
            Assert.Equal(2ul, config.Rev);
            Assert.Equal(2ul, config.RevEpoch);
        }

        [Fact]
        public void Test_Higher_Rev_Lower_Epoch_Config()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config_higher_rev_lower_epoch.json",
                InternalSerializationContext.Default.BucketConfig);
            Assert.Equal(2ul, config.Rev);
            Assert.Equal(1ul, config.RevEpoch);
        }

        [Fact]
        public void Test_Higher_Rev_Higher_No_Epoch_Config()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config_higher_rev_no_epoch.json",
                InternalSerializationContext.Default.BucketConfig);
            Assert.Equal(2ul, config.Rev);
            Assert.Equal(0ul, config.RevEpoch);
        }

        [Fact]
        public void Test_Lower_Rev_Higher_Epoch_Config()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config_lower_rev_higher_epoch.json",
                InternalSerializationContext.Default.BucketConfig);
            Assert.Equal(1ul, config.Rev);
            Assert.Equal(2ul, config.RevEpoch);
        }

        [Fact]
        public void Test_Lower_Rev_Lower_Epoch_Config()
        {
            var config =
                ResourceHelper.ReadResource(@"Documents\Configs\config_lower_rev_lower_epoch.json",
                    InternalSerializationContext.Default.BucketConfig);
            Assert.Equal(1ul, config.Rev);
            Assert.Equal(1ul, config.RevEpoch);
        }

        [Fact]
        public void Test_Lower_Rev_No_Epoch_Config()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config_lower_rev_no_epoch.json",
                InternalSerializationContext.Default.BucketConfig);
            Assert.Equal(1ul, config.Rev);
            Assert.Equal(0ul, config.RevEpoch);
        }


        [Fact]
        public void Test_IsUpdated_Old_Config_Is_Null()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config_higher_rev_higher_epoch.json",
                InternalSerializationContext.Default.BucketConfig);
            Assert.True(config.IsNewerThan(null));
        }

        [Theory]
        [InlineData(null, "config_higher_rev_no_epoch.json", true)]
        [InlineData("config_higher_rev_no_epoch.json", "config_lower_rev_no_epoch.json", false)]
        [InlineData("config_lower_rev_no_epoch.json", "config_higher_rev_no_epoch.json", true)]
        [InlineData("config_higher_rev_higher_epoch.json", "config_lower_rev_higher_epoch.json", false)]
        [InlineData("config_higher_rev_no_epoch.json", "config_higher_rev_higher_epoch.json", true)]
        [InlineData("config_lower_rev_lower_epoch.json", "config_higher_rev_higher_epoch.json", true)]
        [InlineData("config_lower_rev_higher_epoch.json", "config_higher_rev_lower_epoch.json", false)]
        public void Test_Compare_Config_Revisions_And_Epochs(string oldConfigResource, string newConfigResource, bool newConfigIsHigher)
        {
            var oldConfig = oldConfigResource == null
                ? null
                : ResourceHelper.ReadResource(oldConfigResource, InternalSerializationContext.Default.BucketConfig);
            var newConfig = ResourceHelper.ReadResource(newConfigResource, InternalSerializationContext.Default.BucketConfig);

            if (newConfigIsHigher)
            {
                Assert.True(newConfig.IsNewerThan(oldConfig));
            }
            else
            {
                Assert.False(newConfig.IsNewerThan(oldConfig));
            }
        }

        [Theory]
        [InlineData("config_higher_rev_higher_epoch.json")]
        [InlineData("config_higher_rev_lower_epoch.json")]
        [InlineData("config_higher_rev_no_epoch.json")]
        [InlineData("config_lower_rev_lower_epoch.json")]
        [InlineData("config_lower_rev_higher_epoch.json")]
        [InlineData("config_lower_rev_no_epoch.json")]
        public void IsNewer_ReturnsFalse_When_Comparing_Same_Config(string configResource)
        {
            var config = ResourceHelper.ReadResource(configResource, InternalSerializationContext.Default.BucketConfig);
            Assert.False(config.IsNewerThan(config));
        }

        [Fact]
        public void VBucketMap_DoesDeserialize()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-with-ffmaps.json", InternalSerializationContext.Default.BucketConfig);

            Assert.Equal(1024, config.VBucketServerMap.VBucketMap.Length);
            Assert.All(config.VBucketServerMap.VBucketMap, p =>
            {
                Assert.Equal(2, p.Length);

                Assert.Contains(p, q => q == 0);
                Assert.Contains(p, q => q == 1);
            });

            Assert.Equal(1024, config.VBucketServerMap.VBucketMapForward.Length);
            Assert.All(config.VBucketServerMap.VBucketMapForward, p =>
            {
                Assert.Equal(2, p.Length);
            });
        }

        [Fact]
        public void Test_AppTelemetryPath_Included()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-apptelemetry-path.json",
                InternalSerializationContext.Default.BucketConfig);
            var node1 = config.NodesExt[0];
            var node2 = config.NodesExt[1];
            var node3 = config.NodesExt[2];
            Assert.Equal("/_appTelemetry", node1.AppTelemetryPath);
            Assert.Null(node2.AppTelemetryPath);
            Assert.Null(node3.AppTelemetryPath);
        }

        private static BucketConfig ReadAppTelemetryConfig() =>
            ResourceHelper.ReadResource(@"Documents\Configs\config-apptelemetry-multiple.json",
                InternalSerializationContext.Default.BucketConfig);

        [Fact]
        public void GetAppTelemetryUris_No_Paths_Returns_Empty()
        {
            var config = ReadAppTelemetryConfig();
            foreach (var nodeExt in config.NodesExt)
            {
                nodeExt.AppTelemetryPath = null;
            }

            Assert.Empty(config.GetAppTelemetryUris(false, NetworkResolution.Default));
        }

        [Fact]
        public void GetAppTelemetryUris_Only_Nodes_With_Path()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-apptelemetry-path.json",
                InternalSerializationContext.Default.BucketConfig);

            var uris = config.GetAppTelemetryUris(false, NetworkResolution.Default);

            Assert.Equal(new[] { new Uri("ws://172.17.0.2:8091/_appTelemetry") }, uris);
        }

        [Fact]
        public void GetAppTelemetryUris_All_Nodes_Use_Mgmt_Port()
        {
            var config = ReadAppTelemetryConfig();

            var uris = config.GetAppTelemetryUris(false, NetworkResolution.Default);

            Assert.Equal(new[]
            {
                new Uri("ws://172.17.0.2:8091/_appTelemetry"),
                new Uri("ws://172.17.0.3:8091/_appTelemetry"),
                new Uri("ws://172.17.0.4:8091/_appTelemetry")
            }, uris);
        }

        [Fact]
        public void GetAppTelemetryUris_Tls_Uses_Wss_And_MgmtSsl_Port()
        {
            var config = ReadAppTelemetryConfig();

            var uris = config.GetAppTelemetryUris(true, NetworkResolution.Default);

            Assert.Equal(new[]
            {
                new Uri("wss://172.17.0.2:18091/_appTelemetry"),
                new Uri("wss://172.17.0.3:18091/_appTelemetry"),
                new Uri("wss://172.17.0.4:18091/_appTelemetry")
            }, uris);
        }

        [Theory]
        [InlineData(NetworkResolution.External)]
        [InlineData(NetworkResolution.Auto)]
        public void GetAppTelemetryUris_Uses_Alternate_Address_When_Selected(string networkResolution)
        {
            var config = ReadAppTelemetryConfig();

            Assert.Equal(new[]
            {
                new Uri("ws://192.168.132.234:32790/_appTelemetry"),
                new Uri("ws://192.168.132.235:32814/_appTelemetry"),
                new Uri("ws://192.168.132.236:32838/_appTelemetry")
            }, config.GetAppTelemetryUris(false, networkResolution));

            Assert.Equal(new[]
            {
                new Uri("wss://192.168.132.234:32773/_appTelemetry"),
                new Uri("wss://192.168.132.235:32797/_appTelemetry"),
                new Uri("wss://192.168.132.236:32821/_appTelemetry")
            }, config.GetAppTelemetryUris(true, networkResolution));
        }

        [Fact]
        public void GetAppTelemetryUris_Alternate_Address_Uses_Only_Its_Own_Ports()
        {
            var config = ReadAppTelemetryConfig();
            config.NodesExt[0].AlternateAddresses[NetworkResolution.External].Ports = null;
            config.NodesExt[1].AlternateAddresses[NetworkResolution.External].Ports.Mgmt = 0;

            var uris = config.GetAppTelemetryUris(false, NetworkResolution.External);

            Assert.Equal(new[]
            {
                new Uri("ws://192.168.132.234:8091/_appTelemetry"),
                new Uri("ws://192.168.132.236:32838/_appTelemetry")
            }, uris);
        }

        [Fact]
        public void GetAppTelemetryUris_Brackets_IPv6_Hosts()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-apptelemetry-path.json",
                InternalSerializationContext.Default.BucketConfig);
            config.NodesExt[0].Hostname = "fd63:6f75:6368:2068:1471:75ff:fe25:a8be";

            var uri = Assert.Single(config.GetAppTelemetryUris(false, NetworkResolution.Default));

            Assert.Equal("ws://[fd63:6f75:6368:2068:1471:75ff:fe25:a8be]:8091/_appTelemetry", uri.ToString());
        }

        [Fact]
        public void Filter_ServerGroups_and_Indexes()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json", InternalSerializationContext.Default.BucketConfig);

            var groupAndIndexes = config.ServerGroupNodeIndexes;

            Assert.Equal(2, groupAndIndexes.Count);
            Assert.Equal([0, 1], groupAndIndexes["group_1"]);
            Assert.Equal([2, 3], groupAndIndexes["group_2"]);
        }

        [Fact]
        public void ServerGroupNodeIndexes_ExternalNetwork_MapsGroupsToAlternateAddresses()
        {
            var config = ReadServerGroupConfig(bucketNodes: 3,
            [
                NodeExt(0, "group-1", external: true),
                NodeExt(1, "group-2", external: true),
                NodeExt(2, "group-1", external: true)
            ]);
            config.NetworkResolution = NetworkResolution.External;
            Assert.Equal(["203.0.113.10:32210", "203.0.113.11:32210", "203.0.113.12:32210"],
                config.VBucketServerMap.ServerList);

            var groups = config.ServerGroupNodeIndexes;

            Assert.Equal(2, groups.Count);
            Assert.Equal([0, 2], groups["group-1"]);
            Assert.Equal([1], groups["group-2"]);
        }

        [Fact]
        public void ServerGroupNodeIndexes_DataNodeOutsideBucket_IsNotMapped()
        {
            // node-3 joined the cluster but not yet the bucket, as at the start of a rebalance.
            var config = ReadServerGroupConfig(bucketNodes: 3,
            [
                NodeExt(0, "group-1"),
                NodeExt(1, "group-2"),
                NodeExt(2, "group-1"),
                NodeExt(3, "group-2")
            ]);

            var groups = config.ServerGroupNodeIndexes;

            Assert.Equal(2, groups.Count);
            Assert.Equal([0, 2], groups["group-1"]);
            Assert.Equal([1], groups["group-2"]);
        }

        [Fact]
        public void ServerGroupNodeIndexes_TlsOnlyCluster_MapsEveryNode()
        {
            // A server that offers only TLS sends port 0 in the server list.
            var config = ReadServerGroupConfig(bucketNodes: 2,
                [NodeExt(0, "group-1", tlsOnly: true), NodeExt(1, "group-2", tlsOnly: true)], kvPort: 0);

            var groups = config.ServerGroupNodeIndexes;

            Assert.Equal(2, groups.Count);
            Assert.Equal([0], groups["group-1"]);
            Assert.Equal([1], groups["group-2"]);
        }

        [Fact]
        public void ServerGroupNodeIndexes_NodeWithoutServerGroup_IsSkipped()
        {
            var config = ReadServerGroupConfig(bucketNodes: 3,
                [NodeExt(0, "group-1"), NodeExt(1, serverGroup: null), NodeExt(2, "group-1")]);

            var groups = config.ServerGroupNodeIndexes;

            var group = Assert.Single(groups);
            Assert.Equal("group-1", group.Key);
            Assert.Equal([0, 2], group.Value);
        }

        [Fact]
        public void ServerGroupNodeIndexes_IsBuiltOncePerConfig()
        {
            var config = ReadServerGroupConfig(bucketNodes: 2, [NodeExt(0, "group-1"), NodeExt(1, "group-2")]);

            Assert.Same(config.ServerGroupNodeIndexes, config.ServerGroupNodeIndexes);
        }

        /// <summary>
        /// A bucket config as a 7.x server sends it. The first <paramref name="bucketNodes"/> entries of
        /// <paramref name="nodesExt"/> serve the bucket, the others are cluster nodes outside it.
        /// </summary>
        private static BucketConfig ReadServerGroupConfig(int bucketNodes, string[] nodesExt, int kvPort = 11210)
        {
            var nodes = Enumerable.Range(0, bucketNodes).Select(i =>
                $$$"""{"couchApiBase":"http://node-{{{i}}}.example.com:8092/default","hostname":"node-{{{i}}}.example.com:8091","ports":{"direct":{{{kvPort}}}}}""");
            var serverList = Enumerable.Range(0, bucketNodes).Select(i => $"\"node-{i}.example.com:{kvPort}\"");
            var vBucketMap = Enumerable.Range(0, 8).Select(vb => $"[{vb % bucketNodes},{(vb + 1) % bucketNodes}]");

            var json = $$"""
                {
                  "rev": 1042, "revEpoch": 1, "name": "default", "nodeLocator": "vbucket",
                  "uuid": "a5b9b3a3e4f1d1c0b6a2f0e2c4d8e7f1", "bucketCapabilitiesVer": "",
                  "bucketCapabilities": ["collections", "durableWrite", "subdoc.ReplicaRead", "couchapi", "cbhello", "xattr"],
                  "nodes": [{{string.Join(",", nodes)}}],
                  "nodesExt": [{{string.Join(",", nodesExt)}}],
                  "vBucketServerMap": {
                    "hashAlgorithm": "CRC", "numReplicas": 1,
                    "serverList": [{{string.Join(",", serverList)}}],
                    "vBucketMap": [{{string.Join(",", vBucketMap)}}]
                  }
                }
                """;
            return System.Text.Json.JsonSerializer.Deserialize(json, InternalSerializationContext.Default.BucketConfig);
        }

        private static string NodeExt(int index, string serverGroup, bool external = false, bool tlsOnly = false)
        {
            var services = tlsOnly
                ? """{"mgmtSSL":18091,"kvSSL":11207,"capiSSL":18092}"""
                : """{"mgmt":8091,"mgmtSSL":18091,"kv":11210,"kvSSL":11207,"capi":8092,"capiSSL":18092,"projector":9999}""";
            var group = serverGroup is null ? "" : $$""","serverGroup":"{{serverGroup}}" """;
            var alternate = external
                ? $$$""","alternateAddresses":{"external":{"ports":{"mgmt":32091,"kv":32210,"kvSSL":32207},"hostname":"203.0.113.{{{10 + index}}}"}}"""
                : "";
            return $$"""{"services":{{services}},"hostname":"node-{{index}}.example.com","nodeUUID":"node-uuid-{{index}}"{{group}}{{alternate}}}""";
        }

        [Fact]
        public void Test_BucketConfig_With_Product()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-with-analytics-prod.json", InternalSerializationContext.Default.BucketConfig);
            Assert.Equal("analytics", config.Product);
        }

        [Fact]
        public void Test_BucketConfig_Without_Product()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-apptelemetry-multiple.json", InternalSerializationContext.Default.BucketConfig);
            Assert.Null(config.Product);
        }

        [Theory]
        [InlineData(ClusterCapabilities.SCORE_FUSION, true)]
        [InlineData(ClusterCapabilities.VECTOR_SEARCH, true)]
        [InlineData(ClusterCapabilities.SEARCH_UDF_QUERY, true)]
        [InlineData("search.somethingElse", false)]
        public void Test_HasClusterCap(string capability, bool expected)
        {
            var config = new BucketConfig
            {
                ClusterCapabilities = new Dictionary<string, IEnumerable<string>>
                {
                    ["search"] = new[] { "vectorSearch", "scopedSearchIndex", "scoreFusion", "udfQuery" }
                }
            };

            Assert.Equal(expected, config.HasClusterCap(capability));
        }

        [Fact]
        public void Test_HasClusterCap_When_No_Capabilities_In_Config()
        {
            var config = new BucketConfig();

            Assert.False(config.HasClusterCap(ClusterCapabilities.SCORE_FUSION));
        }
    }
}
