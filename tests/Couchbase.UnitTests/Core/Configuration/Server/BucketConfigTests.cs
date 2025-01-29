using System;
using System.Collections.Concurrent;
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

            var options = new ClusterOptions();
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
                    new TypedRedactor(RedactionLevel.None),
                    endPoint,
                    server,
                    NoopRequestTracer.Instance,
                    new Mock<IOperationConfigurator>().Object)
                {
                    Owner = new FakeBucket("default", new ClusterOptions())
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
                    new TypedRedactor(RedactionLevel.None), endPoint, nodesExt,
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
            var clusterOptions = new ClusterOptions().WithConnectionString("couchbase://UNIT_TEST_NO_SUCH_HOST");
            var config = ResourceHelper.ReadResource(@"Documents\Configs\missing-management.json",
                InternalSerializationContext.Default.BucketConfig);
            var clusterContext = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
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

        [Fact]
        public void Test_AppTelemetryPath_Random_Round_Robin()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\config-apptelemetry-multiple.json",
                InternalSerializationContext.Default.BucketConfig);

            var node1 = config.GetAppTelemetryPath(0);
            var node2 = config.GetAppTelemetryPath(1);
            Assert.NotEqual(node1, node2);

            var node3 = config.GetAppTelemetryPath(2);
            Assert.NotEqual(node1, node3);
            Assert.NotEqual(node2, node3);

            var node4 = config.GetAppTelemetryPath(3);
            Assert.Equal(node1, node4);


            var node5 = config.GetAppTelemetryPath(4, true);
            var node6 = config.GetAppTelemetryPath(5, true);
            Assert.NotEqual(node5, node6);

            var node7 = config.GetAppTelemetryPath(6, true);
            Assert.NotEqual(node5, node7);
            Assert.NotEqual(node6, node7);
        }

        [Fact]
        public void Filter_ServerGroups_and_Indexes()
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json", InternalSerializationContext.Default.BucketConfig);

            var hostnamesAndIndex = config.HostnamesAndIndex;
            var hostnamesAndGroup = config.HostnameAndServerGroup;
            var groupAndIndexes = config.ServerGroupNodeIndexes;

            Assert.Equal(4, hostnamesAndIndex.Count);
            Assert.Equal(0, hostnamesAndIndex["192.168.56.102"]);
            Assert.Equal(1, hostnamesAndIndex["192.168.56.101"]);
            Assert.Equal(2, hostnamesAndIndex["192.168.56.103"]);
            Assert.Equal(3, hostnamesAndIndex["192.168.56.104"]);

            Assert.Equal(4, hostnamesAndGroup.Count);
            Assert.Equal("group_1", hostnamesAndGroup["192.168.56.102"]);
            Assert.Equal("group_1", hostnamesAndGroup["192.168.56.101"]);
            Assert.Equal("group_2", hostnamesAndGroup["192.168.56.103"]);
            Assert.Equal("group_2", hostnamesAndGroup["192.168.56.104"]);

            Assert.Equal(2, groupAndIndexes.Count);
            Assert.Equal([0, 1], groupAndIndexes["group_1"]);
            Assert.Equal([2, 3], groupAndIndexes["group_2"]);
        }
    }
}
