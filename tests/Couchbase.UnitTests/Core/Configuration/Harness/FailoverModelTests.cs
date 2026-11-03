#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Configuration.Model;
using Xunit;
using Xunit.Abstractions;
using static Couchbase.UnitTests.Core.Configuration.Harness.ClusterModelTests;
using static Couchbase.UnitTests.Core.Configuration.Harness.ClusterProfileTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// Failover and recovery in the cluster model. The docker profile and the failover captures of server 8.6 give the
    /// expected maps and counts.
    /// </summary>
    public class FailoverModelTests(ITestOutputHelper output)
    {
        private const string Node0 = "node-0.example.com";
        private const string Node1 = "node-1.example.com";
        private const string Node2 = "node-2.example.com";
        private const string Node3 = "node-3.example.com";

        public static TheoryData<int> Seeds() => new() { 0, 1, 2, 3, 4 };

        /// <summary>
        /// The failed host stays in nodesExt for a moment, leaves it, and comes back with the same node UUID.
        /// </summary>
        [Fact]
        public void HardFailoverThenFullRecovery_EachStageMatchesItsExample()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;

            model.Apply(new FailOver([Node2]));
            AssertMatches(model, model.Cluster, "cluster-3nodes-stable.json");
            AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-node-outside-bucket.json");

            model.Apply(new EjectNodes([Node2]));
            AssertMatches(model, model.Cluster, "cluster-2nodes-stable.json");
            AssertMatches(model, model.Bucket(bucket), "bucket-2nodes-stable.json");
            AssertMatches(model, model.Cluster, "cluster-2nodes-no-this-node.json", Node2);
            AssertMatches(model, model.Bucket(bucket), "bucket-2nodes-no-this-node.json", Node2);

            model.Apply(new RecoverNode(Node2, Delta: false));
            AssertMatches(model, model.Cluster, "cluster-3nodes-stable.json");
            AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-node-outside-bucket.json");

            model.Apply(new AddToBucket(bucket, [Node2]));
            AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-stable.json");
            Run(model, Rebalance(model, bucket, [], seed: 0), e =>
            {
                if (e is StartRebalance) AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-forward-map.json");
            });
            AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-stable.json");
        }

        /// <summary>
        /// The 1 node examples come from the end of the unsafe failover run, so they are at epoch 2.
        /// </summary>
        [Fact]
        public void UnsafeFailover_MatchesOneNodeExamplesAtEpochTwo()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;

            model.Apply(new BumpEpoch());
            model.Apply(new FailOver([Node1, Node2]));
            model.Apply(new EjectNodes([Node1, Node2]));

            AssertMatches(model, model.Cluster, "cluster-1nodes-stable.json");
            AssertMatches(model, model.Bucket(bucket), "bucket-1nodes-stable.json");
            Run(model, Rebalance(model, bucket, [], seed: 0));
            AssertMatches(model, model.Bucket(bucket), "bucket-1nodes-stable.json");
        }

        [Fact]
        public void FailOver_GivesEveryMapOneVersion()
        {
            var model = ClusterModel.Create(Docker, buckets: ["a", "b"]);
            var nodes = model.Cluster.Nodes;

            var changed = model.Apply(new FailOver([Node1]));

            Assert.Equal([null, "a", "b"], changed.Select(m => m.Bucket));
            Assert.All(Latest(model), map => Assert.Equal(new ConfigVersion(1, 2), map.Version));
            Assert.Equal(nodes, model.Cluster.Nodes);
            Assert.Equal([Node1], model.FailedHosts);
        }

        [Fact]
        public void FailOver_DropsCopiesOnHostAndPromotesItsReplicas()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            var before = model.Bucket(bucket);

            model.Apply(new FailOver([Node1]));

            var after = model.Bucket(bucket);
            Assert.Equal([Node0, Node2], after.BucketNodes);
            Assert.Equal([Node0, Node2, Node1], after.Nodes.Select(n => n.Hostname));
            Assert.Null(after.ForwardMap);
            Assert.All(VBuckets(before), v =>
            {
                var expected = before.Chain(v).Where(h => h != Node1).Concat(Enumerable.Repeat<string?>(null, 2)).Take(2);
                Assert.Equal(expected, after.Chain(v));
            });
            Assert.All(VBuckets(before).Where(v => before.ActiveHost(v) == Node1),
                v => Assert.Equal(before.Chain(v)[1], after.ActiveHost(v)));

            var config = model.Render(after, Node1).Build();
            Assert.Equal([Node0, Node2, Node1], config.NodesExt.Select(n => n.Hostname));
            Assert.Equal([Node1], config.NodesExt.Where(n => n.ThisNode).Select(n => n.Hostname));
            Assert.DoesNotContain(config.VBucketServerMap.ServerList, s => s.StartsWith(Node1, StringComparison.Ordinal));
            Assert.DoesNotContain(config.Nodes, n => n.Hostname.StartsWith(Node1, StringComparison.Ordinal));
        }

        [Fact]
        public void FailOver_LastHost_KeepsChainsWithoutItAsTheSameRows()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            var before = model.Bucket(bucket);

            model.Apply(new FailOver([Node2]));

            var after = model.Bucket(bucket);
            Assert.All(VBuckets(before).Where(v => !before.Chain(v).Contains(Node2)),
                v => Assert.Same(before.VBucketMap[v], after.VBucketMap[v]));
            Assert.All(VBuckets(before).Where(v => before.Chain(v).Contains(Node2)),
                v => Assert.Equal((short)-1, after.VBucketMap[v][1]));
        }

        /// <summary>
        /// A bucket without the failed host gets a version with the same chains.
        /// </summary>
        [Fact]
        public void FailOver_BucketWithoutHost_GetsOnlyNewVersion()
        {
            var model = ClusterModel.Create(Docker, buckets: ["a", "b"]);
            Run(model, Rebalance(model, "b", [Node2], seed: 0));
            model.Apply(new RemoveFromBucket("b", [Node2]));
            var before = model.Bucket("b");

            model.Apply(new FailOver([Node2]));

            var after = model.Bucket("b");
            Assert.Equal(model.Cluster.Version, after.Version);
            Assert.Same(before.VBucketMap, after.VBucketMap);
            Assert.Equal(before.BucketNodes, after.BucketNodes);
        }

        [Fact]
        public void UnsafeFailover_LeavesChainsWithNoCopyThenRebalanceCreatesThem()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            var before = model.Bucket(bucket);
            var lost = VBuckets(before).Where(v => !before.Chain(v).Contains(Node0)).ToList();

            model.Apply(new BumpEpoch());
            model.Apply(new FailOver([Node1, Node2]));

            var after = model.Bucket(bucket);
            Assert.Equal(new ConfigVersion(2, 3), after.Version);
            Assert.Equal([Node0], after.BucketNodes);
            Assert.Equal([Node0, Node1, Node2], after.Nodes.Select(n => n.Hostname));
            Assert.All(VBuckets(after), v => Assert.Equal(lost.Contains(v) ? new string?[] { null, null } : [Node0, null], after.Chain(v)));

            model.Apply(new EjectNodes([Node1, Node2]));
            model.Apply(new StartRebalance(bucket, []));
            Assert.Equal(lost, model.PendingMoves(bucket));
            Run(model, Rebalance(model, bucket, [], seed: 0).Skip(1));

            var rebalanced = model.Bucket(bucket);
            Assert.Equal(2UL, rebalanced.Version.Epoch);
            Assert.All(VBuckets(rebalanced), v => Assert.Equal([Node0, null], rebalanced.Chain(v)));
        }

        [Fact]
        public void BumpEpochAndBumpAll_ChangeOnlyVersionOfEveryMap()
        {
            var model = ClusterModel.Create(Docker, buckets: ["a", "b"], rev: 10);
            model.Apply(new StartRebalance("a", [Node2]));
            var before = Latest(model).ToList();

            Assert.Equal([null, "a", "b"], model.Apply(new BumpEpoch()).Select(m => m.Bucket));
            Assert.All(Latest(model), map => Assert.Equal(new ConfigVersion(2, 12), map.Version));
            Assert.Equal([null, "a", "b"], model.Apply(new BumpAll()).Select(m => m.Bucket));
            Assert.All(Latest(model), map => Assert.Equal(new ConfigVersion(2, 13), map.Version));

            foreach (var (old, now) in before.Zip(Latest(model), (old, now) => (old, now)))
            {
                Assert.Equal(Leaves(model.Render(old, Node0).Rev(13).Epoch(2).ToJson()), Leaves(model.Render(now, Node0).ToJson()));
                Assert.Same(old.VBucketMap, now.VBucketMap);
                Assert.Same(old.ForwardMap, now.ForwardMap);
            }

            model.Apply(new BumpRev("b"));
            Assert.Equal(new ConfigVersion(2, 14), model.Bucket("b").Version);
            Assert.Equal(new ConfigVersion(2, 13), model.Bucket("a").Version);
        }

        [Fact]
        public void StartGracefulFailover_SwapsEachActiveOnHostWithItsReplica()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            var before = model.Bucket(bucket);
            var actives = VBuckets(before).Where(v => before.ActiveHost(v) == Node1).ToList();

            Assert.Equal([bucket], model.Apply(new StartGracefulFailover(bucket, [Node1])).Select(m => m.Bucket));

            var started = model.Bucket(bucket);
            Assert.Same(before.VBucketMap, started.VBucketMap);
            Assert.Equal(actives, model.PendingMoves(bucket));
            Assert.All(VBuckets(started), v =>
            {
                if (actives.Contains(v)) Assert.Equal([before.Chain(v)[1], Node1], started.ForwardChain(v));
                else Assert.Same(before.VBucketMap[v], started.ForwardMap![v]);
            });

            Run(model, GracefulFailover(model, bucket, Node1, seed: 0).Skip(1).TakeWhile(e => e is not FailOver));
            var drained = model.Bucket(bucket);
            Assert.Null(drained.ForwardMap);
            Assert.DoesNotContain(VBuckets(drained), v => drained.ActiveHost(v) == Node1);
            Assert.Equal(VBuckets(before).Count(v => before.Chain(v).Contains(Node1)),
                VBuckets(drained).Count(v => drained.Chain(v).Contains(Node1)));

            model.Apply(new FailOver([Node1]));
            var failed = model.Bucket(bucket);
            Assert.All(VBuckets(failed), v => Assert.Equal(drained.ActiveHost(v), failed.ActiveHost(v)));
        }

        [Fact]
        public void StartGracefulFailover_InvalidState_Throws()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;

            AssertRejected(model, new StartGracefulFailover("missing", [Node1]), "The model has no bucket missing");
            AssertRejected(model, new StartGracefulFailover(bucket, []), "The event lists no host");
            AssertRejected(model, new StartGracefulFailover(bucket, ["node-9.example.com"]), "node-9.example.com is not in the bucket");
            AssertRejected(model, new StartGracefulFailover(bucket, [Node1, Node2]), "has no replica on another host");

            model.Apply(new StartGracefulFailover(bucket, [Node1]));
            AssertRejected(model, new StartGracefulFailover(bucket, [Node2]), "A rebalance is running");
            AssertRejected(model, new FailOver([Node1]), $"A rebalance is running in bucket {bucket}");
        }

        [Fact]
        public void FailOver_InvalidHosts_Throws()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;

            AssertRejected(model, new FailOver([]), "The event lists no host");
            AssertRejected(model, new FailOver([Node1, Node1]), "The event lists a host twice");
            AssertRejected(model, new FailOver(["node-9.example.com"]), "node-9.example.com is not in nodesExt");
            AssertRejected(model, new FailOver([Node0, Node1, Node2]), $"Bucket {bucket} would keep no node");

            model.Apply(new FailOver([Node1]));
            AssertRejected(model, new FailOver([Node1]), $"{Node1} is failed already");
            model.Apply(new EjectNodes([Node1]));
            AssertRejected(model, new FailOver([Node1]), $"{Node1} is not in nodesExt");

            model.Apply(new StartRebalance(bucket, []));
            AssertRejected(model, new FailOver([Node2]), $"A rebalance is running in bucket {bucket}");
        }

        [Fact]
        public void FullRecovery_BringsHostBackWithSameNodeAndNoCopies()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            var node = model.Cluster.Node(Node1)!;
            model.Apply(new FailOver([Node1]));
            model.Apply(new EjectNodes([Node1]));
            var failed = model.Bucket(bucket);
            Assert.Equal([Node1], model.FailedHosts);

            model.Apply(new RecoverNode(Node1, Delta: false));

            Assert.All(Latest(model), map => Assert.Equal(new ConfigVersion(1, 4), map.Version));
            Assert.Equal([Node0, Node1, Node2], model.Cluster.Nodes.Select(n => n.Hostname));
            Assert.Same(node, model.Cluster.Node(Node1));
            Assert.Empty(model.FailedHosts);
            var recovered = model.Bucket(bucket);
            Assert.Equal([Node0, Node2], recovered.BucketNodes);
            Assert.Equal([Node0, Node2, Node1], recovered.Nodes.Select(n => n.Hostname));
            Assert.Same(failed.VBucketMap, recovered.VBucketMap);
            var rendered = model.Render(recovered, Node1).Build().NodesExt.Last();
            Assert.Equal((Node1, node.NodeUuid, true), (rendered.Hostname, rendered.NodeUuid, rendered.ThisNode));

            model.Apply(new AddToBucket(bucket, [Node1]));

            var added = model.Bucket(bucket);
            Assert.Equal([Node0, Node1, Node2], added.BucketNodes);
            Assert.All(VBuckets(added), v => Assert.Equal(failed.Chain(v), added.Chain(v)));
            Run(model, Rebalance(model, bucket, [], seed: 0));
            var chains = model.Bucket(bucket).Chains;
            AssertWithinOne(CopiesPerServer(chains, 3, chain => chain.Take(1)));
            AssertWithinOne(CopiesPerServer(chains, 3, chain => chain.Skip(1)));
        }

        [Fact]
        public void DeltaRecovery_GivesHostBackAReplicaOfEachVBucketItHeld()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            var start = model.Bucket(bucket);
            var held = VBuckets(start).Where(v => start.Chain(v).Contains(Node1)).ToList();
            model.Apply(new FailOver([Node1]));
            model.Apply(new EjectNodes([Node1]));
            var failed = model.Bucket(bucket);

            Assert.Equal([null, bucket], model.Apply(new RecoverNode(Node1, Delta: true)).Select(m => m.Bucket));

            Assert.All(Latest(model), map => Assert.Equal(new ConfigVersion(1, 4), map.Version));
            Assert.Equal([Node0, Node1, Node2], model.Cluster.Nodes.Select(n => n.Hostname));
            Assert.Empty(model.FailedHosts);
            var recovered = model.Bucket(bucket);
            Assert.Equal([Node0, Node1, Node2], recovered.BucketNodes);
            Assert.Equal([Node0, Node1, Node2], recovered.Nodes.Select(n => n.Hostname));
            Assert.Null(recovered.ForwardMap);
            Assert.All(VBuckets(recovered), v =>
            {
                Assert.Equal(held.Contains(v) ? new[] { failed.ActiveHost(v), Node1 } : failed.Chain(v), recovered.Chain(v));
                Assert.Equal(start.Chain(v).OrderBy(h => h, StringComparer.Ordinal), recovered.Chain(v).OrderBy(h => h, StringComparer.Ordinal));
            });

            model.Apply(new StartRebalance(bucket, []));
            Assert.Contains(model.PendingMoves(bucket), v => model.Bucket(bucket).ForwardChain(v)![0] == Node1);
        }

        [Fact]
        public void DeltaRecovery_BeforeEject_KeepsNodesExtOrder()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            var start = model.Bucket(bucket);
            model.Apply(new FailOver([Node1]));

            model.Apply(new RecoverNode(Node1, Delta: true));

            var recovered = model.Bucket(bucket);
            Assert.Equal([Node0, Node1, Node2], recovered.BucketNodes);
            Assert.Equal([Node0, Node1, Node2], recovered.Nodes.Select(n => n.Hostname));
            Assert.All(VBuckets(recovered), v => Assert.Equal(start.Chain(v).OrderBy(h => h, StringComparer.Ordinal),
                recovered.Chain(v).OrderBy(h => h, StringComparer.Ordinal)));
        }

        [Fact]
        public void RecoverNode_InvalidState_Throws()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;

            AssertRejected(model, new RecoverNode(Node1, Delta: true), $"{Node1} is not failed");

            // Node 3 holds no copy, so every chain keeps its replica and node 2 can fail over gracefully.
            model.Apply(new AddNodes(1));
            model.Apply(new AddToBucket(bucket, [Node3]));
            model.Apply(new FailOver([Node3]));
            model.Apply(new StartGracefulFailover(bucket, [Node2]));
            AssertRejected(model, new RecoverNode(Node3, Delta: true), $"A rebalance is running in bucket {bucket}");

            model.Apply(new RecoverNode(Node3, Delta: false));
            AssertRejected(model, new RecoverNode(Node3, Delta: false), $"{Node3} is not failed");
        }

        [Fact]
        public void FailedHost_RejoinOrAddToBucket_Throws()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            model.Apply(new FailOver([Node1, Node2]));
            model.Apply(new EjectNodes([Node1]));

            AssertRejected(model, new RejoinNode(Node1), $"{Node1} is failed. RecoverNode brings it back");
            AssertRejected(model, new RejoinNode(Node2), $"{Node2} is failed. RecoverNode brings it back");
            AssertRejected(model, new AddToBucket(bucket, [Node2]), $"{Node2} is failed. RecoverNode brings it back");
        }

        /// <summary>
        /// The next rebalance in any bucket forgets the failed hosts, like the server ejects them.
        /// </summary>
        [Fact]
        public void StartRebalance_ForgetsFailedHosts()
        {
            var model = ClusterModel.Create(Docker, buckets: ["a", "b"]);
            model.Apply(new FailOver([Node1, Node2]));
            model.Apply(new EjectNodes([Node1]));
            Assert.Equal([Node1, Node2], model.FailedHosts);

            model.Apply(new StartRebalance("b", []));

            Assert.Empty(model.FailedHosts);
            AssertRejected(model, new RecoverNode(Node1, Delta: false), $"{Node1} is not failed");
            AssertRejected(model, new RecoverNode(Node2, Delta: true), $"{Node2} is not failed");
            Assert.NotNull(model.Cluster.Node(Node2));
            Assert.All(model.Buckets, b => Assert.False(model.Bucket(b).IsBucketNode(Node2)));

            model.Apply(new EjectNodes([Node2]));
            model.Apply(new RejoinNode(Node1));

            var rejoined = model.Cluster.Node(Node1)!;
            Assert.Equal(3, rejoined.Member);
            Assert.Equal(Docker.NodeShape.NodeUuid(3), rejoined.NodeUuid);
            Assert.Equal([Node0, Node1], model.Cluster.Nodes.Select(n => n.Hostname));
        }

        /// <summary>
        /// The server keeps nodesExt and serverList in hostname order, so a host that rejoins goes back in the middle and
        /// the later bucket nodes get new indexes.
        /// </summary>
        [Fact]
        public void RejoinNode_LowHostname_JoinsInHostnameOrderAndShiftsIndexes()
        {
            var model = ClusterModel.Create(Docker);
            var bucket = model.Profile.BucketName;
            Run(model, Rebalance(model, bucket, [Node1], seed: 0));
            model.Apply(new RemoveFromBucket(bucket, [Node1]));
            model.Apply(new EjectNodes([Node1]));
            model.Apply(new AddNodes(1));
            model.Apply(new AddToBucket(bucket, [Node3]));
            model.Apply(new StartRebalance(bucket, []));
            model.Apply(new MoveVBuckets(bucket, model.PendingMoves(bucket).Take(3).ToList()));
            var before = model.Bucket(bucket);
            Assert.Contains(before.ForwardMap!, chain => chain.Contains((short)1));
            Assert.Contains(VBuckets(before), v => ReferenceEquals(before.VBucketMap[v], before.ForwardMap![v]));

            model.Apply(new RejoinNode(Node1));
            Assert.Equal([Node0, Node1, Node2, Node3], model.Cluster.Nodes.Select(n => n.Hostname));
            Assert.Equal([Node0, Node2, Node3, Node1], model.Bucket(bucket).Nodes.Select(n => n.Hostname));
            model.Apply(new AddToBucket(bucket, [Node1]));

            var after = model.Bucket(bucket);
            Assert.Equal([Node0, Node1, Node2, Node3], after.BucketNodes);
            Assert.Equal([Node0, Node1, Node2, Node3], after.Nodes.Select(n => n.Hostname));
            Assert.All(VBuckets(after), v =>
            {
                Assert.Equal(before.Chain(v), after.Chain(v));
                Assert.Equal(before.ForwardChain(v), after.ForwardChain(v));
                if (ReferenceEquals(before.VBucketMap[v], before.ForwardMap![v])) Assert.Same(after.VBucketMap[v], after.ForwardMap![v]);
            });
        }

        [Fact]
        public void Nodes_KeepOrdinalHostnameOrder()
        {
            var model = ClusterModel.Create(Docker, nodes: 10);

            model.Apply(new AddNodes(2));

            Assert.Equal(["node-0", "node-1", "node-10", "node-11", "node-2"],
                model.Cluster.Nodes.Take(5).Select(n => n.Hostname.Split('.')[0]));
            Assert.Equal([10, 11], model.Cluster.Nodes.Skip(2).Take(2).Select(n => n.Member));
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void HardFailover_OneOfThree_PromotesActivesLikeTheCapture(int seed)
        {
            var model = ClusterModel.Create(Docker, seed: seed);
            var before = model.Bucket(model.Profile.BucketName);

            model.Apply(new FailOver([Node1]));

            var after = model.Bucket(model.Profile.BucketName);
            var promoted = VBuckets(after).Count(v => after.ActiveHost(v) != before.ActiveHost(v));
            var missing = VBuckets(after).Count(v => after.Chain(v).Contains(null));
            output.WriteLine($"seed {seed} promoted {promoted} chains with -1 {missing}");
            Assert.InRange(promoted, 341, 342);
            Assert.InRange(missing, 682, 684);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void GracefulFailover_ForwardMapChangesOnlyActivesOnHost(int seed)
        {
            var model = ClusterModel.Create(Docker, seed: seed);
            var bucket = model.Profile.BucketName;
            var before = model.Bucket(bucket);

            model.Apply(new StartGracefulFailover(bucket, [Node1]));

            var pending = model.PendingMoves(bucket);
            output.WriteLine($"seed {seed} forward chains changed {pending.Count}");
            Assert.InRange(pending.Count, 341, 342);
            Assert.All(pending, v => Assert.Equal(Node1, before.ActiveHost(v)));
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void DeltaRecovery_AfterGracefulFailover_GivesBackEveryCopy(int seed)
        {
            var model = ClusterModel.Create(Docker, seed: seed);
            var bucket = model.Profile.BucketName;
            var start = model.Bucket(bucket);
            Run(model, GracefulFailover(model, bucket, Node1, seed));
            model.Apply(new EjectNodes([Node1]));

            model.Apply(new RecoverNode(Node1, Delta: true));

            var recovered = model.Bucket(bucket);
            var copies = VBuckets(recovered).Count(v => recovered.Chain(v).Contains(Node1));
            output.WriteLine($"seed {seed} copies restored {copies}");
            Assert.InRange(copies, 682, 684);
            Assert.Equal(VBuckets(start).Count(v => start.Chain(v).Contains(Node1)), copies);
            Assert.DoesNotContain(VBuckets(recovered), v => recovered.ActiveHost(v) == Node1);
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void UnsafeFailover_TwoOfThree_LosesAboutOneThird(int seed)
        {
            var model = ClusterModel.Create(Docker, seed: seed);
            model.Apply(new BumpEpoch());

            model.Apply(new FailOver([Node1, Node2]));

            var after = model.Bucket(model.Profile.BucketName);
            var lost = VBuckets(after).Count(v => after.Chain(v).All(h => h is null));
            output.WriteLine($"seed {seed} chains with no copy {lost}");
            Assert.InRange(lost, 330, 352);
        }

        /// <summary>
        /// Like the hard-full capture. The failed host leaves nodesExt and comes back with a full recovery. Then a
        /// rebalance fills it.
        /// </summary>
        [Fact]
        public async Task HardFailoverThenFullRecovery_WithSdk_FollowsEveryVersion()
        {
            var model = ClusterModel.Create(Docker, seed: 1);
            var bucketName = model.Profile.BucketName;
            await using var harness = ConfigHarness.Create(model.Render(model.Cluster, null));
            var bucket = await harness.OpenBucketAsync(model.Render(model.Bucket(bucketName), null));
            var server = new ModelServer(model, harness.Server);
            server.Seed();

            var differences = await DriveAsync(harness, server, bucket, model, HardFailoverThenFullRecovery(model, bucketName, Node2, seed: 1));

            Assert.Empty(differences);
            Assert.Contains(Node2, harness.Bucket(bucketName).Nodes);
        }

        /// <summary>
        /// Like the graceful-delta capture. Node 1 comes back between the other two, so the index of node 2 shifts.
        /// </summary>
        [Fact]
        public async Task GracefulFailoverThenDeltaRecovery_WithSdk_FollowsEveryVersion()
        {
            var model = ClusterModel.Create(Docker, seed: 2);
            var bucketName = model.Profile.BucketName;
            await using var harness = ConfigHarness.Create(model.Render(model.Cluster, null));
            var bucket = await harness.OpenBucketAsync(model.Render(model.Bucket(bucketName), null));
            var server = new ModelServer(model, harness.Server);
            server.Seed();

            var differences = await DriveAsync(harness, server, bucket, model, GracefulFailoverThenDeltaRecovery(model, bucketName, Node1, seed: 2));

            Assert.Empty(differences);
            Assert.Equal([Node0, Node1, Node2], harness.Bucket(bucketName).Nodes);
        }

        /// <summary>
        /// Like the unsafe capture. The epoch goes up and two of three hosts fail. Then the rebalance creates the lost
        /// vbuckets.
        /// </summary>
        [Fact]
        public async Task UnsafeFailover_WithSdk_FollowsEveryVersion()
        {
            var model = ClusterModel.Create(Docker, seed: 3);
            var bucketName = model.Profile.BucketName;
            await using var harness = ConfigHarness.Create(model.Render(model.Cluster, null));
            var bucket = await harness.OpenBucketAsync(model.Render(model.Bucket(bucketName), null));
            var server = new ModelServer(model, harness.Server);
            server.Seed();

            var differences = await DriveAsync(harness, server, bucket, model, UnsafeFailover(model, bucketName, [Node1, Node2], seed: 3));

            Assert.Empty(differences);
            Assert.Equal(2UL, harness.Bucket(bucketName).Version!.Value.Epoch);
            Assert.Equal([Node0], harness.Bucket(bucketName).Nodes);
        }

        /// <summary>
        /// The forward map swaps the actives off the host, the moves take 1 or 2 vbuckets each, and the failover comes
        /// once the forward map is cleared. Read <see cref="ClusterModelTests.ScaleOut"/> for how to apply the events.
        /// </summary>
        internal static IEnumerable<ModelEvent> GracefulFailover(ClusterModel model, string bucket, string host, int seed)
        {
            yield return new StartGracefulFailover(bucket, [host]);
            var random = new Random(seed);
            while (model.PendingMoves(bucket) is { Count: > 0 } pending)
            {
                var take = random.Next(1, 3);
                yield return new MoveVBuckets(bucket, pending.OrderBy(_ => random.Next()).Take(take).OrderBy(v => v).ToList());
            }
            yield return new FinishRebalance(bucket);
            yield return new FailOver([host]);
        }

        internal static IEnumerable<ModelEvent> HardFailoverThenFullRecovery(ClusterModel model, string bucket, string host, int seed)
        {
            yield return new FailOver([host]);
            yield return new EjectNodes([host]);
            yield return new BumpAll();
            yield return new RecoverNode(host, Delta: false);
            yield return new AddToBucket(bucket, [host]);
            foreach (var e in Rebalance(model, bucket, [], seed)) yield return e;
        }

        internal static IEnumerable<ModelEvent> GracefulFailoverThenDeltaRecovery(ClusterModel model, string bucket, string host, int seed)
        {
            yield return new BumpAll();
            foreach (var e in GracefulFailover(model, bucket, host, seed)) yield return e;
            yield return new EjectNodes([host]);
            yield return new BumpAll();
            yield return new RecoverNode(host, Delta: true);
            foreach (var e in Rebalance(model, bucket, [], seed)) yield return e;
        }

        internal static IEnumerable<ModelEvent> UnsafeFailover(ClusterModel model, string bucket, IReadOnlyList<string> hosts, int seed)
        {
            yield return new BumpEpoch();
            yield return new FailOver(hosts);
            yield return new EjectNodes(hosts);
            foreach (var e in Rebalance(model, bucket, [], seed)) yield return e;
        }

        private static IEnumerable<int> VBuckets(ModelMap map) => Enumerable.Range(0, map.VBucketCount);
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
