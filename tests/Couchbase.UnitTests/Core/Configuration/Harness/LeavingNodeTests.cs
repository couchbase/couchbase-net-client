#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Configuration.Model;
using Couchbase.Test.Common.Configuration.Scenarios;
using Xunit;
using static Couchbase.Test.Common.Configuration.Scenarios.ModelSequences;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigHarnessTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// A node that leaves the cluster ends its connections, and SELECT_BUCKET fails on the new ones, like in the captures.
    /// </summary>
    public class LeavingNodeTests
    {
        private const string Node0 = "node-0.example.com";
        private const string Node1 = "node-1.example.com";
        private const string Node2 = "node-2.example.com";
        private const string Node3 = "node-3.example.com";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Theory]
        [InlineData(ClusterProfile.CapellaPublic, ResponseStatus.Eaccess)]
        [InlineData(ClusterProfile.CapellaPrivateLink, ResponseStatus.KeyNotFound)]
        [InlineData(ClusterProfile.Docker, ResponseStatus.Eaccess)]
        public void LeftNodeSelectBucketStatus_IsTheReplyInTheCapture(string profile, ResponseStatus status)
        {
            Assert.Equal(status, ClusterProfile.Load(profile).LeftNodeSelectBucketStatus);
        }

        /// <summary>
        /// The fake pool never reconnects, so the connections stay open. A new connection gets HELLO, and SELECT_BUCKET
        /// fails until the node serves a bucket map that lists it.
        /// </summary>
        [Fact]
        public async Task Leave_InFakeMode_KeepsConnectionsAndFailsSelectBucketUntilABucketMapListsTheNode()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var node = (IConnectionInitializer)harness.Context.Nodes.Single(n => n.Owner == bucket && n.EndPoint.Host == Node0);
            var open = harness.Server.ConnectionsOn(Node0);

            harness.Server.Leave(Node0, ResponseStatus.Eaccess);
            var refused = harness.Server.Connect(node.EndPoint);
            await node.InitializeConnectionAsync(refused);
            await Assert.ThrowsAsync<AuthenticationFailureException>(() => node.SelectBucketAsync(refused, BucketName));
            harness.Server.Serve(Node0, BucketMap(11));
            var accepted = harness.Server.Connect(node.EndPoint);
            await node.InitializeConnectionAsync(accepted);
            await node.SelectBucketAsync(accepted, BucketName);

            Assert.NotEmpty(open);
            Assert.All(open, c => Assert.False(c.IsClosed));
            Assert.Null(refused.Bucket);
            Assert.Equal(BucketName, accepted.Bucket);
            Assert.False(harness.Server.HasLeft(Node0));
            Assert.Equal(1, harness.Server.Counters(Node0).SelectBucketsFailed);
            Assert.Equal(1, harness.Server.Received.Count(r => r.Session == refused.Id && r.OpCode == OpCode.Helo));
        }

        /// <summary>
        /// Node 0 serves the bucket and the Private Link seed. It ends both kinds of connection. The bucket pool gets
        /// HELLO on each new connection and the select bucket failure, and the seed pool reconnects to node 1.
        /// Once node 0 serves a bucket map with itself in it again, a new bucket connection selects the bucket.
        /// </summary>
        [Fact]
        public async Task Leave_InRealMode_EndsEveryConnectionAndSdkReconnects()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink).RealConnections().StartAsync();
            var server = scenario.Harness.Server;
            var bucket = scenario.Model.Profile.BucketName;
            await scenario.Harness.SettleAsync();
            var before = server.ConnectionsOn(Node0);
            Assert.Contains(before, c => c.IsSeed);
            Assert.Contains(before, c => c.Bucket == bucket);
            var received = server.Received.Count;

            server.Leave(Node0, ResponseStatus.KeyNotFound);
            Assert.All(before, c => Assert.True(c.IsClosed));
            await scenario.Harness.WaitUntilAsync(
                () => server.Counters(Node0).SelectBucketsFailed > 0 && server.ConnectionsOn(Node1).Any(c => c.IsSeed),
                Timeout, "The SDK reconnecting to node 0 and through the seed");
            await scenario.Harness.SettleAsync();

            AssertHelloOnEachConnection(server, NewRequests(server, Node0, before, received));
            Assert.All(server.ConnectionsOn(Node0), c => Assert.Null(c.Bucket));
            Assert.DoesNotContain(server.ConnectionsOn(Node0), c => c.IsSeed);

            server.Serve(Node0, scenario.Model.Render(scenario.Model.Bucket(bucket), Node0));
            var node = (ClusterNode)scenario.Bucket().Nodes.Single(n => server.NodeAt(n.EndPoint) == Node0);
            await node.ConnectionPool.ScaleAsync(1);

            Assert.False(server.HasLeft(Node0));
            Assert.Contains(server.ConnectionsOn(Node0), c => c.Bucket == bucket);
        }

        /// <summary>
        /// With two buckets the node leaves with the second one. A rejoin alone does not end the leave, a bucket that adds
        /// the node does.
        /// </summary>
        [Fact]
        public void ModelServer_NodeLeavesWithItsLastBucket()
        {
            var model = ClusterModel.Create(ClusterModelTests.Docker, nodes: 4, buckets: ["travel", "audit"]);
            var fake = new FakeClusterServer();
            var server = new ModelServer(model, fake);
            server.Seed();

            var left = Play(model, server, ScaleIn(model, model.Buckets, [Node3]));
            Assert.Equal([$"{Node3} at RemoveFromBucket audit [{Node3}]"], left);
            Assert.True(fake.HasLeft(Node3));

            Play(model, server, [new RejoinNode(Node3)]);
            Assert.True(fake.HasLeft(Node3));
            Play(model, server, [new AddToBucket("travel", [Node3])]);
            Assert.False(fake.HasLeft(Node3));
        }

        /// <summary>
        /// A failed node does not leave at the failover. It leaves at the rebalance that forgets it, and a recovered node
        /// does not leave.
        /// </summary>
        [Fact]
        public void ModelServer_FailedNodeLeavesAtTheRebalanceThatForgetsIt()
        {
            var model = ClusterModel.Create(ClusterModelTests.Docker, nodes: 4);
            var fake = new FakeClusterServer();
            var server = new ModelServer(model, fake);
            server.Seed();

            var left = Play(model, server, FailOverHosts(model, model.Buckets, [Node1], Failover.Hard));
            left.AddRange(Play(model, server, Recover(model, model.Buckets, Node1, Recovery.Delta)));
            Assert.Empty(left);

            left.AddRange(Play(model, server, FailOverHosts(model, model.Buckets, [Node2], Failover.Hard)));
            Assert.Empty(left);
            Assert.False(fake.HasLeft(Node2));
            left.AddRange(Play(model, server, ScaleOut(model, model.Buckets, 1)));

            Assert.Equal([$"{Node2} at StartRebalance {model.Buckets[0]} leaving []"], left);
            Assert.False(fake.HasLeft(Node1));
        }

        /// <summary>
        /// A node that lags gets no delivery, so it leaves when it catches up.
        /// </summary>
        [Fact]
        public void ModelServer_LaggingNodeLeavesWhenItCatchesUp()
        {
            var model = ClusterModel.Create(ClusterModelTests.Docker, nodes: 4);
            var fake = new FakeClusterServer();
            var server = new ModelServer(model, fake);
            server.Seed();

            var left = Play(model, server, ScaleIn(model, model.Buckets, [Node3]), except: [Node3]);
            Assert.Empty(left);
            Assert.False(fake.HasLeft(Node3));

            server.Deliver(Node3);

            Assert.True(fake.HasLeft(Node3));
            Assert.Empty(server.Leave());
        }

        /// <summary>
        /// Like the captures, the leaving node ends its connections right before the map without it in the bucket. The
        /// SDK reconnects, gets HELLO and then the select bucket failure of the profile. The scale in and the wait are
        /// unchecked. The bucket refresher can fetch from the node that left, whose pool has no connection, and drop the
        /// version, so the SDK can stay on the map from before the removal.
        /// </summary>
        [Theory]
        [InlineData(ClusterProfile.Docker)]
        [InlineData(ClusterProfile.CapellaPrivateLink)]
        public async Task ScaleIn_LeavingNodeEndsItsConnections_AndSdkGetsSelectBucketFailure(string profile)
        {
            await using var scenario = await Scenario.On(profile).RealConnections().StartAsync();
            var server = scenario.Harness.Server;
            await scenario.Harness.SettleAsync();
            var before = server.ConnectionsOn(Node2);
            Assert.NotEmpty(before);
            var received = server.Received.Count;

            await scenario.Unchecked(s => s.ScaleIn("node-2").Wait(TimeSpan.FromSeconds(1))).RunAsync();

            Assert.True(server.HasLeft(Node2));
            Assert.All(before, c => Assert.True(c.IsClosed));
            var reconnects = NewRequests(server, Node2, before, received);
            AssertHelloOnEachConnection(server, reconnects);
            var selects = reconnects.Count(r => r.OpCode == OpCode.SelectBucket);
            Assert.True(selects > 0, $"The SDK did not reconnect to the node that left. {server.Counters(Node2)}");
            Assert.Equal(selects, server.Counters(Node2).SelectBucketsFailed);
            await scenario.Harness.WaitUntilAsync(() => server.ConnectionsOn(Node2).Count == 0, Timeout,
                "The SDK closing its connections to node 2");
        }

        /// <summary>
        /// On Private Link the SDK never drops its seed node, so node 0 still has the seed connections after its failover.
        /// It ends them at the next rebalance, which forgets it, and the seed pool reconnects to node 1.
        /// </summary>
        [Fact]
        public async Task HardFailover_FailedNodeKeepsItsConnectionsUntilTheRebalanceThatForgetsIt()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink).RealConnections().StartAsync();
            var server = scenario.Harness.Server;
            var seeds = server.ConnectionsOn(Node0).Where(c => c.IsSeed).ToList();
            Assert.NotEmpty(seeds);

            await scenario.FailOver("node-0", Failover.Hard).RunAsync();

            Assert.False(server.HasLeft(Node0));
            Assert.All(seeds, c => Assert.False(c.IsClosed));

            await scenario.ScaleOut(1).RunAsync();

            Assert.True(server.HasLeft(Node0));
            Assert.All(seeds, c => Assert.True(c.IsClosed));
            Assert.DoesNotContain(server.ConnectionsOn(Node0), c => c.IsSeed);
            Assert.Contains(server.ConnectionsOn(Node1), c => c.IsSeed);
            Assert.Equal([Node1, Node2, Node3], scenario.View().Nodes);
        }

        /// <summary>
        /// Applies each event, then leaves and delivers like the scenario runner, and lists each host that left with the event.
        /// </summary>
        private static List<string> Play(ClusterModel model, ModelServer server, IEnumerable<ModelEvent> events,
            IReadOnlyCollection<string>? except = null)
        {
            var left = new List<string>();
            foreach (var e in events)
            {
                model.Apply(e);
                left.AddRange(server.Leave(except).Select(h => $"{h} at {e}"));
                server.DeliverAll(except: except);
            }
            return left;
        }

        private static void AssertHelloOnEachConnection(FakeClusterServer server, IEnumerable<ServerRequest> requests)
        {
            var hellos = server.Received.Where(r => r.OpCode == OpCode.Helo).Select(r => r.Session).ToHashSet();
            Assert.All(requests.Select(r => r.Session).Distinct(), session => Assert.Contains(session, hellos));
        }

        /// <summary>
        /// The requests the host received after the first <paramref name="received"/> ones, on connections other than
        /// <paramref name="before"/>.
        /// </summary>
        private static List<ServerRequest> NewRequests(FakeClusterServer server, string host,
            IReadOnlyList<FakeClusterServer.Session> before, int received)
        {
            var old = before.Select(c => c.Id).ToHashSet();
            return server.Received.Skip(received).Where(r => r.Host == host && !old.Contains(r.Session)).ToList();
        }
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
