#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Configuration.Model;
using Couchbase.Test.Common.Configuration.Scenarios;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// Private Link on the external network. Every node has the same external host and its own kvSSL port. The SDK
    /// starts from that host on 11207, which is in no map.
    /// </summary>
    public class PrivateLinkTests
    {
        private const string Endpoint = "private-endpoint.example.com";
        private const string Node0 = "node-0.example.com";
        private const string Node1 = "node-1.example.com";
        private const string Node2 = "node-2.example.com";
        private const string Node3 = "node-3.example.com";
        private const string Node4 = "node-4.example.com";

        private static readonly HostEndpointWithPort Seed = new(Endpoint, 11207);

        private static ClusterProfile PrivateLink => ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);

        [Fact]
        public void NodeAt_FindsNodeByAlternateHostAndPort()
        {
            var model = ClusterModel.Create(PrivateLink);
            var server = new FakeClusterServer { Tls = true };
            server.SetMap(model.Render(model.Cluster, null));

            // Member 0 has kvSSL 11210, member 1 has 11209 and member 2 has 11208, like the capture.
            Assert.Equal(Node0, server.NodeAt(new HostEndpointWithPort(Endpoint, 11210)));
            Assert.Equal(Node1, server.NodeAt(new HostEndpointWithPort(Endpoint, 11209)));
            Assert.Equal(Node2, server.NodeAt(new HostEndpointWithPort(Endpoint, 11208)));
            Assert.Null(server.NodeAt(Seed));
            Assert.Equal(Node1, server.NodeAt(new HostEndpointWithPort(Node1, 11207)));
        }

        [Fact]
        public void Connect_ToSeedEndpoint_GoesToSeedNodeAndIsMarkedAsSeed()
        {
            var model = ClusterModel.Create(PrivateLink, buckets: []);
            var server = new FakeClusterServer { Tls = true };
            server.SetMap(model.Render(model.Cluster, null));

            var first = server.Connect(Seed);
            var node = server.Connect(new HostEndpointWithPort(Endpoint, 11209));
            model.Apply(new EjectNodes([Node0]));
            server.Serve(Node1, model.Render(model.Cluster, Node1));
            var afterEject = server.Connect(Seed);
            server.SeedNode = Node2;
            var chosen = server.Connect(Seed);

            Assert.Equal((Node0, true, Seed), (first.Host, first.IsSeed, first.Address));
            Assert.Equal((Node1, false), (node.Host, node.IsSeed));
            Assert.Equal((Node1, true), (afterEject.Host, afterEject.IsSeed));
            Assert.Equal((Node2, true), (chosen.Host, chosen.IsSeed));
            Assert.Equal([first], server.ConnectionsOn(Node0));
            Assert.Equal([node, afterEject], server.ConnectionsOn(Node1));
        }

        /// <summary>
        /// The harness creates one node per map node, so each SDK node connects to its own kvSSL port.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task NodesOnOneHost_GetTheirOwnMapsFetchesCountersAndFaults(ConnectionMode mode)
        {
            var model = ClusterModel.Create(PrivateLink, nodes: 2, buckets: []);
            await using var harness = ConfigHarness.Create(model.Render(model.Cluster, null), ConnectThroughEndpoint, mode: mode);
            model.Apply(new BumpRev(null));
            harness.Server.SetMap(model.Render(model.Cluster, null), Node1);
            harness.Server.FailFetchesAfter(1, Node0);
            var node0 = SdkNode(harness, Node0);
            var node1 = SdkNode(harness, Node1);

            var fromNode0 = await node0.GetClusterMap();
            var fromNode1 = await node1.GetClusterMap();
            var failed = await node0.GetClusterMap();

            Assert.Equal([(Endpoint, 11210, Node0), (Endpoint, 11209, Node1)],
                harness.Nodes.Select(n => (n.Hostname, n.EndPoint.Port, n.Name)));
            Assert.Equal(new ConfigVersion(1, 1), fromNode0.ConfigVersion);
            Assert.Equal(new ConfigVersion(1, 2), fromNode1.ConfigVersion);
            Assert.Null(failed);
            Assert.Equal(2, harness.Server.FetchCount(Node0));
            Assert.Equal(1, harness.Server.FetchCount(Node1));
            Assert.Null(harness.Server.Fetches.Last().Served);
            Assert.All(harness.Server.Fetches, f => Assert.False(f.Seed));
            var connections = mode == ConnectionMode.Real ? 2 : 1;
            Assert.Equal(connections, harness.Server.Counters(Node0).ConnectionsOpened);
            Assert.Equal(connections, harness.Server.Counters(Node1).ConnectionsOpened);
            Assert.Equal(harness.Server.Counters(Node1).RequestsAnswered + 1, harness.Server.Counters(Node0).RequestsAnswered);
        }

        /// <summary>
        /// Like the capture, the SDK connects to couchbases:// on the shared host, so TLS is on, and network resolution
        /// auto picks the external network.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task StartAsync_ConnectsThroughSharedHostLikeTheCapture(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink).Connections(mode).StartAsync();
            var options = scenario.Harness.Options;

            Assert.Equal(BootstrapMode.Sdk, scenario.Harness.Bootstrap);
            Assert.Equal($"couchbases://{Endpoint}", options.ConnectionString);
            Assert.True(options.EffectiveEnableTls);
            Assert.Equal(NetworkResolution.External, options.EffectiveNetworkResolution);
            var bootstrapFetch = scenario.Harness.Server.Fetches[0];
            Assert.Equal((Node0, true, null), (bootstrapFetch.Host, bootstrapFetch.Seed, bootstrapFetch.Bucket));
            Assert.Equal([Node0, Node1, Node2], scenario.View().Nodes);
            Assert.Equal([11208, 11209, 11210], scenario.Bucket().Nodes.Select(n => n.EndPoint.Port).OrderBy(p => p));
            Assert.All(scenario.Bucket().Nodes, n => Assert.Equal(Endpoint, n.EndPoint.Host));
        }

        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task ScaleOut_FollowsEveryVersion(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink).Connections(mode).StartAsync();

            await scenario.ScaleOut(2).RunAsync();

            Assert.Equal([Node0, Node1, Node2, Node3, Node4], scenario.View().Nodes);
            Assert.Empty(ScenarioCheck.WrongActives(scenario.View(), scenario.Model.Bucket(scenario.Model.Profile.BucketName)));
        }

        /// <summary>
        /// The node that leaves is the one behind the seed endpoint.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task ScaleInOfSeedNode_FollowsEveryVersion(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink, nodes: 4).Connections(mode).StartAsync();
            Assert.Contains(scenario.Harness.Server.ConnectionsOn(Node0), s => s.IsSeed);

            await scenario.ScaleIn("node-0").RunAsync();

            Assert.Equal([Node1, Node2, Node3], scenario.View().Nodes);
        }

        /// <summary>
        /// The seed endpoint routes to node 0. Node 0 ends the seed connections when it leaves, and the seed pool
        /// reconnects to another node. The scale in and the wait are unchecked, as in <see cref="LeavingNodeTests"/>.
        /// </summary>
        [Fact]
        public async Task ScaleInOfSeedNode_EndsSeedConnections_AndSeedReconnectsToAnotherNode()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink, nodes: 4).RealConnections().StartAsync();
            var server = scenario.Harness.Server;
            var seeds = server.ConnectionsOn(Node0).Where(s => s.IsSeed).ToList();
            Assert.NotEmpty(seeds);

            await scenario.Unchecked(s => s.ScaleIn("node-0").Wait(TimeSpan.FromSeconds(1))).RunAsync();

            Assert.True(server.HasLeft(Node0));
            Assert.All(seeds, s => Assert.True(s.IsClosed));
            Assert.DoesNotContain(server.ConnectionsOn(Node0), s => s.IsSeed);
            Assert.Contains(server.ConnectionsOn(Node1), s => s.IsSeed);
        }

        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task HardFailover_FollowsEveryVersion(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink).Connections(mode).StartAsync();

            await scenario.FailOver("node-1", Failover.Hard).RunAsync();

            Assert.Equal([Node0, Node2], scenario.View().Nodes);
            Assert.Equal([Node1], scenario.Model.FailedHosts);
        }

        /// <summary>
        /// Every node has the same host, so a check by host alone would pass. The check names each node from its port.
        /// </summary>
        [Fact]
        public async Task WrongActives_WithActivesOfTwoNodesSwapped_ListsEveryVBucketOfBoth()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink).StartAsync();
            var latest = scenario.Model.Bucket(scenario.Model.Profile.BucketName);
            var swapped = latest.Chains.Select(chain => chain.Select(i => i switch { 0 => (short)1, 1 => (short)0, _ => i }).ToArray()).ToArray();

            scenario.Harness.Publish(scenario.Model.Render(latest, null).Rev(latest.Version.Revision + 1).VBucketMap(swapped));
            await scenario.Harness.DrainAsync();

            var both = latest.BucketNodes.Take(2).ToList();
            Assert.Equal(Enumerable.Range(0, latest.VBucketCount).Where(v => both.Contains(latest.ActiveHost(v)!)),
                ScenarioCheck.WrongActives(scenario.View(), latest));
            Assert.Equal(both[1], scenario.View().Active(Enumerable.Range(0, latest.VBucketCount).First(v => latest.ActiveHost(v) == both[0])));
        }

        private static void ConnectThroughEndpoint(ClusterOptions options)
        {
            options.ConnectionString = $"couchbases://{Endpoint}";
            options.NetworkResolution = NetworkResolution.Auto;
        }

        private static IClusterNode SdkNode(ConfigHarness harness, string name) =>
            harness.Context.Nodes.Single(n => harness.Server.NodeAt(n.EndPoint) == name);
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
