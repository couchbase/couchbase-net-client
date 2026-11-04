#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Configuration.Model;
using Couchbase.Test.Common.Configuration.Scenarios;
using Xunit;
using Xunit.Sdk;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// The scenario API. Each scenario drives the SDK with the cluster model and checks the SDK buckets after each change.
    /// </summary>
    public class ScenarioTests
    {
        private const string Node0 = "node-0.example.com";
        private const string Node1 = "node-1.example.com";
        private const string Node2 = "node-2.example.com";
        private const string Node3 = "node-3.example.com";
        private const string Node4 = "node-4.example.com";

        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task StartAsync_OpensEveryBucketOnItsModelMap(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink, nodes: 4)
                .Buckets("travel", "audit")
                .Connections(mode)
                .Configure(o => o.KvTimeout = TimeSpan.FromSeconds(1))
                .StartAsync();

            Assert.Equal(mode, scenario.Harness.Mode);
            Assert.Equal(TimeSpan.FromSeconds(1), scenario.Harness.Options.KvTimeout);
            Assert.Equal(["travel", "audit"], scenario.Model.Buckets);
            Assert.Equal(["audit", "travel"], scenario.Context.Buckets.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var bucket in scenario.Model.Buckets)
            {
                Assert.Equal(scenario.Model.Bucket(bucket).Version, scenario.View(bucket).Version);
                Assert.Equal([Node0, Node1, Node2, Node3], scenario.View(bucket).Nodes);
                Assert.All(scenario.Model.Cluster.Nodes, n => Assert.Equal(new ConfigVersion(1, 1), scenario.Server.Served(n.Hostname, bucket)));
            }
        }

        /// <summary>
        /// The SDK bootstrap from the connection string ends with the nodes, owners, versions and bucket nodes of the
        /// bootstrap the harness does by hand.
        /// </summary>
        [Theory]
        [InlineData(ClusterProfile.Docker, ConnectionMode.Fake)]
        [InlineData(ClusterProfile.Docker, ConnectionMode.Real)]
        [InlineData(ClusterProfile.CapellaPublic, ConnectionMode.Fake)]
        [InlineData(ClusterProfile.CapellaPublic, ConnectionMode.Real)]
        public async Task StartAsync_WithSdkBootstrap_EndsLikeHarnessBootstrap(string profile, ConnectionMode mode)
        {
            await using var byHand = await Scenario.On(profile).Connections(mode).Bootstrap(BootstrapMode.Harness).StartAsync();
            await using var bySdk = await Scenario.On(profile).Connections(mode).StartAsync();

            Assert.Equal(BootstrapMode.Harness, byHand.Harness.Bootstrap);
            Assert.Equal(BootstrapMode.Sdk, bySdk.Harness.Bootstrap);
            Assert.Equal(EndState(byHand), EndState(bySdk));
            Assert.Equal([Node0, Node1, Node2], bySdk.View().Nodes);
        }

        /// <summary>
        /// The added node and the rejoined host stay outside the bucket, so for them only nodesExt changes.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task AddNodesSetServicesAndRejoin_FollowEveryVersion(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic, nodes: 4).Connections(mode).StartAsync();
            var leftMember = scenario.Model.Cluster.Node(Node3)!.Member;

            await scenario
                .AddNodes(1, ["n1ql"])
                .SetServices("node-4", ["index", "n1ql"])
                .SetServices("node-1", ["kv", "n1ql"])
                .ScaleIn("node-3")
                .Rejoin("node-3", ["kv"])
                .RunAsync();

            var rejoined = scenario.Model.Cluster.Node(Node3)!;
            Assert.NotEqual(leftMember, rejoined.Member);
            Assert.False(scenario.Model.Bucket(scenario.Model.Profile.BucketName).IsBucketNode(Node3));
            Assert.Equal([Node0, Node1, Node2], scenario.View().Nodes);
            Assert.Equal(["index", "n1ql"], scenario.Model.Cluster.Node(Node4)!.Services);
        }

        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task HardFailoverThenDeltaRecovery_FollowsEveryVersion(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink, nodes: 4).Connections(mode).StartAsync();

            await scenario.FailOver("node-1", Failover.Hard).Recover("node-1", Recovery.Delta).RunAsync();

            Assert.Equal([Node0, Node1, Node2, Node3], scenario.View().Nodes);
            Assert.Empty(scenario.Model.FailedHosts);
        }

        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task GracefulFailoverThenFullRecovery_FollowsEveryVersion(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker, nodes: 4).Connections(mode).StartAsync();

            await scenario.FailOver("node-3", Failover.Graceful).Recover("node-3", Recovery.Full).RunAsync();

            Assert.Equal([Node0, Node1, Node2, Node3], scenario.View().Nodes);
            Assert.Empty(scenario.Model.FailedHosts);
        }

        /// <summary>
        /// Each point runs once, at the model state it names, after the SDK took the version before it.
        /// </summary>
        [Fact]
        public async Task ScaleOutDuring_RunsEachPointAtItsModelState()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).StartAsync();
            var bucket = scenario.Model.Profile.BucketName;
            var points = new List<Point>();

            await scenario
                .ScaleOut(2, during: at => at
                    .AfterStart(s => s.Do(ctx => points.Add(Point.Of("after start", ctx, bucket))))
                    .Halfway(s => s.Do(ctx => points.Add(Point.Of("halfway", ctx, bucket))))
                    .BeforeFinish(s => s.Do(ctx => points.Add(Point.Of("before finish", ctx, bucket)))))
                .RunAsync();

            Assert.Equal(["after start", "halfway", "before finish"], points.Select(p => p.Name));
            var start = points[0];
            Assert.IsType<StartRebalance>(start.Last);
            Assert.True(start.Pending > 100, $"The rebalance starts with {start.Pending} moves.");
            Assert.True(start.Forward);

            var halfway = points[1];
            var moved = Assert.IsType<MoveVBuckets>(halfway.Last).VBuckets.Count;
            Assert.InRange(halfway.Pending * 2, 1, start.Pending);
            Assert.True((halfway.Pending + moved) * 2 > start.Pending, "Halfway ran after a later move.");

            var finish = points[2];
            Assert.IsType<MoveVBuckets>(finish.Last);
            Assert.Equal(0, finish.Pending);
            Assert.True(finish.Forward);
            Assert.All(points, p => Assert.Equal(p.ModelVersion, p.SdkVersion));
            Assert.Null(scenario.Model.Bucket(bucket).ForwardMap);
        }

        /// <summary>
        /// The points of a graceful failover follow the moves of the actives. The hosts fail over after the last point.
        /// </summary>
        [Fact]
        public async Task GracefulFailoverDuring_RunsBeforeFinishBeforeTheHostFails()
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).StartAsync();
            var bucket = scenario.Model.Profile.BucketName;
            var points = new List<Point>();
            IReadOnlyList<string>? failedAtFinish = null;

            await scenario
                .FailOver("node-1", Failover.Graceful, during: at => at
                    .AfterStart(s => s.Do(ctx => points.Add(Point.Of("after start", ctx, bucket))))
                    .BeforeFinish(s => s.Do(ctx =>
                    {
                        points.Add(Point.Of("before finish", ctx, bucket));
                        failedAtFinish = ctx.Model.FailedHosts;
                    })))
                .RunAsync();

            Assert.IsType<StartGracefulFailover>(points[0].Last);
            Assert.Equal(0, points[1].Pending);
            Assert.True(points[1].Forward);
            Assert.Empty(failedAtFinish!);
            Assert.DoesNotContain(Node1, scenario.View().Nodes);
        }

        /// <summary>
        /// With two buckets the points follow the first bucket that rebalances. A point can hold cluster steps.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task ScaleInDuring_WithTwoBuckets_FollowsFirstBucketAndTakesClusterSteps(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker, nodes: 4).Buckets("travel", "audit").Connections(mode).StartAsync();
            string? startedIn = null;

            await scenario
                .ScaleIn("node-3", during: at => at
                    .AfterStart(s => s.Do(ctx => startedIn = ((StartRebalance)ctx.Model.Applied[ctx.Model.Applied.Count - 1].Event).Bucket))
                    .Halfway(s => s.AddNodes(1)))
                .RunAsync();

            Assert.Equal("travel", startedIn);
            Assert.Equal([Node0, Node1, Node2], scenario.View("audit").Nodes);
            Assert.NotNull(scenario.Model.Cluster.Node(Node4));
        }

        [Fact]
        public async Task FailOverDuring_WithoutGraceful_Throws()
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).StartAsync();

            var error = Assert.Throws<ArgumentException>(() => scenario.FailOver("node-1", Failover.Hard, during: at => at.Halfway(_ => { })));

            Assert.Contains("A Hard failover has no rebalance", error.Message);
        }

        [Fact]
        public async Task NetworkSteps_InFakeMode_ThrowWhenAdded()
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).StartAsync();

            var hang = Assert.Throws<InvalidOperationException>(() => scenario.Hang("node-1"));
            Assert.Throws<InvalidOperationException>(() => scenario.Unchecked(s => s.Stall("node-1")));
            Assert.Throws<InvalidOperationException>(() => scenario.ScaleOut(1, during: at => at.Halfway(s => s.Close("node-1"))));
            Assert.Throws<InvalidOperationException>(() => scenario.Resume("node-1"));
            Assert.Throws<InvalidOperationException>(() => scenario.Refuse("node-1", true));

            Assert.Equal($"Hang {Node1} needs real connections. Call RealConnections in the scenario setup.", hang.Message);
            await scenario.RunAsync();
            Assert.Empty(scenario.Log);
        }

        /// <summary>
        /// The SDK fetches from a random bucket node. A lagging node answers that it has nothing newer, so the SDK asks
        /// another node.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task Lag_HostServesOldVersionsUntilCatchUp(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).Connections(mode).StartAsync();
            var bucket = scenario.Model.Profile.BucketName;
            var start = scenario.Model.Bucket(bucket).Version;
            var pushes = scenario.Harness.Server.Counters(Node2).PushesSent;

            await scenario
                .Lag("node-2")
                .ScaleOut(1)
                .Do(ctx =>
                {
                    Assert.Equal(start, ctx.Server.Served(Node2, bucket));
                    Assert.Equal(new ConfigVersion(1, 1), ctx.Server.Served(Node2, null));
                    Assert.Equal(ctx.Model.Bucket(bucket).Version, ctx.Server.Served(Node0, bucket));
                    Assert.Equal(pushes, ctx.Harness.Server.Counters(Node2).PushesSent);
                })
                .CatchUp("node-2")
                .RunAsync();

            Assert.Equal(scenario.Model.Bucket(bucket).Version, scenario.Server.Served(Node2, bucket));
            Assert.Equal(scenario.Model.Cluster.Version, scenario.Server.Served(Node2, null));
            Assert.True(scenario.Harness.Server.Counters(Node2).PushesSent > pushes);
            Assert.Equal([Node0, Node1, Node2, Node3], scenario.View().Nodes);
        }

        /// <summary>
        /// With no push the nodes serve the new maps and send no notification. The next pushed version brings the SDK
        /// to the latest maps.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task NoPush_ServesMapsWithoutNotifications_ThenPushBringsSdkToLatest(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).Connections(mode).StartAsync();
            var bucket = scenario.Model.Profile.BucketName;
            var hosts = new[] { Node0, Node1, Node2 };
            var pushes = hosts.Select(h => scenario.Harness.Server.Counters(h).PushesSent).ToList();

            await scenario.NoPush().Unchecked(s => s.AddNodes(1)).RunAsync();

            Assert.Equal(pushes, hosts.Select(h => scenario.Harness.Server.Counters(h).PushesSent));
            Assert.All(hosts, h => Assert.Equal(scenario.Model.Bucket(bucket).Version, scenario.Server.Served(h, bucket)));
            Assert.Equal(scenario.Model.Cluster.Version, scenario.Server.Served(Node3, null));

            await scenario.Push().ScaleOut(1).RunAsync();

            Assert.All(hosts.Zip(pushes, (h, before) => scenario.Harness.Server.Counters(h).PushesSent - before), sent => Assert.True(sent > 0));
            Assert.Equal([Node0, Node1, Node2, Node4], scenario.View().Nodes);
        }

        [Fact]
        public async Task Wait_OnVirtualClock_MovesClock()
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).StartAsync();
            var start = scenario.Harness.Clock.GetUtcNow();

            await scenario.Wait(TimeSpan.FromMinutes(2)).RunAsync();

            Assert.Equal(start.AddMinutes(2), scenario.Harness.Clock.GetUtcNow());
            Assert.Equal(["Wait 120 s"], scenario.Log);
        }

        /// <summary>
        /// Like the bug 20 tests, the SDK runs on real time with a small KV timeout.
        /// </summary>
        [Fact]
        public async Task RealTime_WaitsAndChecksOnRealTime()
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker)
                .RealConnections()
                .RealTime()
                .Configure(o => o.KvTimeout = TimeSpan.FromMilliseconds(200))
                .StartAsync();
            var elapsed = Stopwatch.StartNew();

            await scenario.Wait(TimeSpan.FromMilliseconds(300)).ScaleOut(1).RunAsync();

            Assert.Same(TimeProvider.System, scenario.Harness.Clock);
            Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(300), $"The run took {elapsed.Elapsed}.");
            Assert.Equal([Node0, Node1, Node2, Node3], scenario.View().Nodes);
        }

        /// <summary>
        /// Halfway through a scale out, a Get goes to the active node of the model.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task Do_SendsKvOperationMidRun(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).Connections(mode).StartAsync();
            string? active = null;
            var before = 0;

            await scenario
                .ScaleOut(2, during: at => at.Halfway(s => s.Do(async ctx =>
                {
                    var bucket = ctx.Bucket();
                    var vbucket = ((VBucket)bucket.KeyMapper!.MapKey("key-1")).Index;
                    active = ctx.Model.Bucket(bucket.Name).ActiveHost(vbucket);
                    before = ctx.Harness.Server.ReceivedCount(active!, OpCode.Get);
                    await Assert.ThrowsAsync<DocumentNotFoundException>(() => bucket.DefaultCollection().GetAsync("key-1"));
                })))
                .RunAsync();

            Assert.Equal(before + 1, scenario.Harness.Server.ReceivedCount(active!, OpCode.Get));
        }

        /// <summary>
        /// No node gets the new version, so the SDK cannot reach it. The run fails at the end with each difference and
        /// the step log.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task EveryNodeLagging_RunFailsWithDifferenceAndStepLog(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).Connections(mode)
                .Deadline(TimeSpan.FromMilliseconds(200)).StartAsync();

            var error = await Assert.ThrowsAsync<FailException>(() =>
                scenario.Lag("node-0").Lag("node-1").Lag("node-2").AddNodes(1).Wait(TimeSpan.FromSeconds(1)).RunAsync());

            Assert.Equal(string.Join("\n",
                "The SDK differs from the model 2 times.",
                "After AddNodes 1, the SDK did not get default 1/2 in 0.2 s. It has 1/1.",
                "After Wait 1 s, the SDK did not get default 1/2 with no new wait after an earlier miss. It has 1/1.",
                "",
                "Step log",
                $"Lag {Node0}",
                $"Lag {Node1}",
                $"Lag {Node2}",
                "AddNodes 1",
                "  2 AddNodes 1 -> cluster, default",
                "Wait 1 s"), error.Message);
        }

        /// <summary>
        /// No node gets the versions of a scale out. The first check waits for the deadline, and the hundreds of checks
        /// after it do not wait again.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task EveryNodeLagging_StuckSdkCostsOneDeadline(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).Connections(mode)
                .Deadline(TimeSpan.FromSeconds(1)).StartAsync();
            var elapsed = Stopwatch.StartNew();

            var error = await Assert.ThrowsAsync<FailException>(() =>
                scenario.Lag("node-0").Lag("node-1").Lag("node-2").ScaleOut(1).RunAsync());

            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(10), $"The run took {elapsed.Elapsed}.");
            var lines = error.Message.Split('\n');
            Assert.Single(lines, l => l.EndsWith("in 1 s. It has 1/1.", StringComparison.Ordinal));
            Assert.True(lines.Count(l => l.Contains("with no new wait after an earlier miss", StringComparison.Ordinal)) > 100);
        }

        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task Unchecked_WithEveryNodeLagging_RunPassesAndCatchUpIsChecked(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).Connections(mode)
                .Deadline(TimeSpan.FromMilliseconds(200)).StartAsync();

            await scenario
                .Unchecked(s => s.Lag("node-0").Lag("node-1").Lag("node-2").AddNodes(1))
                .RunAsync();
            Assert.Equal(new ConfigVersion(1, 1), scenario.View().Version);

            await scenario.CatchUp("node-0").CatchUp("node-1").CatchUp("node-2").RunAsync();

            Assert.Equal(scenario.Model.Bucket("default").Version, scenario.View().Version);
            Assert.Equal(["Unchecked", $"  Lag {Node0}", $"  Lag {Node1}", $"  Lag {Node2}", "  AddNodes 1",
                "    2 AddNodes 1 -> cluster, default", $"CatchUp {Node0}", $"CatchUp {Node1}", $"CatchUp {Node2}"], scenario.Log);
        }

        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task RunAsyncTwice_RunsOnlyStepsAddedSince(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).Connections(mode).StartAsync();

            await scenario.ScaleOut(1).RunAsync();
            var logged = scenario.Log.Count;
            Assert.Equal([Node0, Node1, Node2, Node3], scenario.View().Nodes);

            await scenario.ScaleIn("node-0").RunAsync();

            Assert.Equal("ScaleOut 1", scenario.Log[0]);
            Assert.Equal($"ScaleIn [{Node0}]", scenario.Log[logged]);
            Assert.Single(scenario.Log, l => l.StartsWith("ScaleOut", StringComparison.Ordinal));
            Assert.Equal([Node1, Node2, Node3], scenario.View().Nodes);
        }

        /// <summary>
        /// A docker cluster grows by one node while another node lags for the second half of the rebalance. Then a node
        /// fails over gracefully and comes back with a delta recovery.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task Docker_ScaleOutWithLagThenGracefulFailoverAndDeltaRecovery(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).Connections(mode).StartAsync();

            await scenario
                .ScaleOut(1, during: at => at
                    .Halfway(s => s.Lag("node-2"))
                    .BeforeFinish(s => s.CatchUp("node-2")))
                .FailOver("node-1", Failover.Graceful)
                .Recover("node-1", Recovery.Delta)
                .RunAsync();

            Assert.Equal([Node0, Node1, Node2, Node3], scenario.View().Nodes);
            Assert.All(scenario.Model.Cluster.Nodes, n => Assert.Equal(scenario.Model.Cluster.Version, scenario.Server.Served(n.Hostname, null)));
        }

        /// <summary>
        /// Two buckets, like the scenario the API was written for, without a hung node.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task CapellaPrivateLink_TwoBuckets_ScaleInScaleOutGracefulFailoverAndDeltaRecovery(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPrivateLink, nodes: 4)
                .Buckets("travel", "audit")
                .Connections(mode)
                .Configure(o => o.KvTimeout = TimeSpan.FromMilliseconds(500))
                .StartAsync();

            await scenario
                .ScaleIn("node-3")
                .ScaleOut(2, services: ["kv", "n1ql"])
                .FailOver("node-2", Failover.Graceful)
                .Recover("node-2", Recovery.Delta)
                .Wait(TimeSpan.FromSeconds(30))
                .RunAsync();

            Assert.Equal([Node0, Node1, Node2, Node4, "node-5.example.com"], scenario.View("travel").Nodes);
            Assert.Equal([Node0, Node1, Node2, Node4, "node-5.example.com"], scenario.View("audit").Nodes);
            Assert.Equal(["kv", "n1ql"], scenario.Model.Cluster.Node(Node4)!.Services);
            Assert.Null(scenario.Model.Cluster.Node(Node3));
        }

        /// <summary>
        /// Node 1 lags for the second half of a scale out. Then the bootstrap node fails over and comes back with a
        /// delta recovery.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task CapellaPublic_ScaleOutWithLagThenHardFailoverAndDeltaRecovery(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).Connections(mode).StartAsync();

            await scenario
                .ScaleOut(2, during: at => at
                    .Halfway(s => s.Lag("node-1"))
                    .BeforeFinish(s => s.CatchUp("node-1")))
                .FailOver("node-0", Failover.Hard)
                .Recover("node-0", Recovery.Delta)
                .RunAsync();

            Assert.Equal([Node0, Node1, Node2, Node3, Node4], scenario.View().Nodes);
            Assert.Empty(scenario.Model.FailedHosts);
        }

        /// <summary>
        /// Node 1 ends every connection halfway through a scale out. The SDK follows every version.
        /// </summary>
        [Fact]
        public async Task CapellaPublic_ConnectionsClosedHalfwayThroughScaleOut_FollowsEveryVersion()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).RealConnections().StartAsync();
            var closed = scenario.Harness.Server.Counters(Node1).ConnectionsClosed;

            await scenario.ScaleOut(2, during: at => at.Halfway(s => s.Close("node-1"))).RunAsync();

            Assert.True(scenario.Harness.Server.Counters(Node1).ConnectionsClosed >= closed + 2);
            Assert.Equal([Node0, Node1, Node2, Node3, Node4], scenario.View().Nodes);
        }

        /// <summary>
        /// Node 1 hangs for the second half of a scale out, and resumes before the rebalance ends. The scale out is
        /// unchecked, because a fetch from the hung node drops the pushed version (bug 20). Once node 1 resumes, the SDK
        /// reaches the model maps.
        /// </summary>
        [Fact]
        public async Task CapellaPublic_NodeHungHalfwayThroughScaleOut_EndsOnModelMaps()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).RealConnections().StartAsync();
            var held = 0;

            await scenario
                .Unchecked(s => s.ScaleOut(2, during: at => at
                    .Halfway(h => h.Hang("node-1"))
                    .BeforeFinish(h => h
                        .Do(ctx => held = ctx.Harness.Server.Counters(Node1).PushesHeld)
                        .Resume("node-1"))))
                .Wait(TimeSpan.FromSeconds(1))
                .RunAsync();

            Assert.True(held > 0, "Node 1 held no notification while it hung.");
            var counters = scenario.Harness.Server.Counters(Node1);
            Assert.Equal(0, counters.PushesHeld);
            Assert.Equal(counters.RequestsReceived, counters.RequestsAnswered);
            Assert.Equal([Node0, Node1, Node2, Node3, Node4], scenario.View().Nodes);
        }

        /// <summary>
        /// Node 1 stops reading for the second half of a scale out. Like a hang, it is unchecked (bug 20).
        /// </summary>
        [Fact]
        public async Task CapellaPublic_NodeStalledHalfwayThroughScaleOut_EndsOnModelMaps()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).RealConnections().StartAsync();

            await scenario
                .Unchecked(s => s.ScaleOut(2, during: at => at
                    .Halfway(h => h.Stall("node-1"))
                    .BeforeFinish(h => h.Resume("node-1"))))
                .Wait(TimeSpan.FromSeconds(1))
                .RunAsync();

            var counters = scenario.Harness.Server.Counters(Node1);
            Assert.Equal(counters.RequestsReceived, counters.RequestsAnswered);
            Assert.Equal([Node0, Node1, Node2, Node3, Node4], scenario.View().Nodes);
        }

        /// <summary>
        /// Node 1 goes down for the second half of a scale out. It closes its connections and refuses new ones until
        /// the last move. The scale out is unchecked, because a fetch from the down node also drops the pushed version.
        /// </summary>
        [Fact]
        public async Task CapellaPublic_NodeDownHalfwayThroughScaleOut_EndsOnModelMaps()
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).RealConnections()
                .Configure(o => o.KvTimeout = TimeSpan.FromMilliseconds(500))
                .StartAsync();
            var closed = scenario.Harness.Server.Counters(Node1).ConnectionsClosed;

            await scenario
                .Unchecked(s => s.ScaleOut(2, during: at => at
                    .Halfway(h => h.Refuse("node-1", true).Close("node-1"))
                    .BeforeFinish(h => h.Refuse("node-1", false))))
                .Wait(TimeSpan.FromSeconds(1))
                .RunAsync();

            Assert.True(scenario.Harness.Server.Counters(Node1).ConnectionsClosed >= closed + 2);
            Assert.Equal([Node0, Node1, Node2, Node3, Node4], scenario.View().Nodes);
        }

        private static List<string> EndState(Scenario scenario)
        {
            var state = scenario.Harness.Nodes.Select(n => $"{n.EndPoint} {n} uuid={n.NodeUuid}").ToList();
            state.Add($"global {scenario.Harness.GlobalVersion}");
            state.AddRange(scenario.Model.Buckets.Select(b => $"{b} {scenario.View(b).Version} [{string.Join(", ", scenario.View(b).Nodes)}]"));
            return state;
        }

        private sealed record Point(string Name, int Pending, bool Forward, ModelEvent Last, ConfigVersion ModelVersion, ConfigVersion? SdkVersion)
        {
            public static Point Of(string name, ScenarioContext context, string bucket)
            {
                var map = context.Model.Bucket(bucket);
                return new Point(name, context.Model.PendingMoves(bucket).Count, map.ForwardMap is not null,
                    context.Model.Applied[context.Model.Applied.Count - 1].Event, map.Version, context.View(bucket).Version);
            }
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
