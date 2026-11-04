#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Configuration.Model;
using Couchbase.Test.Common.Configuration.Scenarios;
using Xunit;
using static Couchbase.Test.Common.Configuration.Scenarios.ModelSequences;
using static Couchbase.UnitTests.Core.Configuration.Harness.ClusterProfileTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    public class ClusterModelTests
    {
        private const string Node0 = "node-0.example.com";
        private const string Node3 = "node-3.example.com";
        private const string Node4 = "node-4.example.com";

        public static TheoryData<string, string, string> StableExamples() => new()
        {
            { ClusterProfile.CapellaPrivateLink, "cluster-3nodes-stable.json", "bucket-3nodes-stable.json" },
            { ClusterProfile.CapellaPublic, "cluster-3nodes-stable.json", "bucket-3nodes-stable.json" },
            { ClusterProfile.Docker, "cluster-3nodes-stable.json", "bucket-3nodes-stable.json" }
        };

        [Theory]
        [MemberData(nameof(StableExamples))]
        public void Render_ThreeNodes_MatchesStableExamples(string profile, string clusterExample, string bucketExample)
        {
            var model = ClusterModel.Create(ClusterProfile.Load(profile));

            AssertMatches(model, model.Cluster, clusterExample);
            AssertMatches(model, model.Bucket(model.Profile.BucketName), bucketExample);
        }

        /// <summary>
        /// The Capella captures have an example at each stage of a scale out from 3 to 5 nodes and of the first scale in.
        /// Private Link removed member 3 first, the public cluster removed member 0 first.
        /// </summary>
        [Theory]
        [InlineData(ClusterProfile.CapellaPrivateLink, Node3)]
        [InlineData(ClusterProfile.CapellaPublic, Node0)]
        public void ScaleOutThenIn_EachStageMatchesItsExample(string profile, string leaving)
        {
            var model = ClusterModel.Create(ClusterProfile.Load(profile));
            var bucket = model.Profile.BucketName;
            var stages = new Dictionary<Type, Action>
            {
                [typeof(AddNodes)] = () => AssertMatches(model, model.Cluster, "cluster-5nodes-mixed-services.json"),
                [typeof(AddToBucket)] = () => AssertMatches(model, model.Bucket(bucket), "bucket-5nodes-mixed-services.json"),
                [typeof(StartRebalance)] = () => AssertMatches(model, model.Bucket(bucket), "bucket-5nodes-forward-map.json")
            };

            Run(model, ScaleOut(model, bucket, 2), e =>
            {
                if (stages.TryGetValue(e.GetType(), out var check)) check();
            });
            AssertMatches(model, model.Bucket(bucket), "bucket-5nodes-stable.json");

            Run(model, ScaleIn(model, bucket, leaving), e =>
            {
                if (e is RemoveFromBucket) AssertMatches(model, model.Bucket(bucket), "bucket-5nodes-node-outside-bucket.json");
            });
            AssertMatches(model, model.Cluster, "cluster-4nodes-no-this-node.json", leaving);
            AssertMatches(model, model.Bucket(bucket), "bucket-4nodes-no-this-node.json", leaving);
            AssertMatches(model, model.Bucket(bucket), "bucket-4nodes-stable.json");
        }

        /// <summary>
        /// The docker capture grows from 2 to 3 data nodes and back. It has one server group and no other service.
        /// </summary>
        [Fact]
        public void DockerScaleOutThenIn_EachStageMatchesItsExample()
        {
            const string node2 = "node-2.example.com";
            var model = ClusterModel.Create(Docker, nodes: 2);
            var bucket = model.Profile.BucketName;
            AssertMatches(model, model.Cluster, "cluster-2nodes-stable.json");
            AssertMatches(model, model.Bucket(bucket), "bucket-2nodes-stable.json");

            model.Apply(new AddNodes(1));
            AssertMatches(model, model.Cluster, "cluster-3nodes-stable.json");
            AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-node-outside-bucket.json");
            model.Apply(new AddToBucket(bucket, [node2]));
            AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-stable.json");
            Run(model, Rebalance(model, bucket, [], seed: 0), e =>
            {
                if (e is StartRebalance) AssertMatches(model, model.Bucket(bucket), "bucket-3nodes-forward-map.json");
            });

            Run(model, Rebalance(model, bucket, [node2], seed: 0));
            model.Apply(new RemoveFromBucket(bucket, [node2]));
            model.Apply(new EjectNodes([node2]));
            AssertMatches(model, model.Cluster, "cluster-2nodes-no-this-node.json", node2);
        }

        [Fact]
        public void Create_GivesEveryMapTheStartVersion()
        {
            var model = ClusterModel.Create(Docker, nodes: 2, buckets: ["a", "b"], epoch: 3, rev: 40);

            Assert.All(Latest(model), map => Assert.Equal(new ConfigVersion(3, 40), map.Version));
            Assert.Equal([Node0, "node-1.example.com"], model.Bucket("a").BucketNodes);
            Assert.Equal(["kv"], model.DefaultServices);
            Assert.Equal(["index", "kv", "n1ql"], ClusterModel.Create(PrivateLink).DefaultServices);
            Assert.Empty(model.Applied);
        }

        [Fact]
        public void AddNodes_GivesClusterAndEveryBucketTheSameNewRev()
        {
            var model = ClusterModel.Create(PrivateLink, buckets: ["a", "b"], rev: 10);

            var changed = model.Apply(new AddNodes(2, ["kv"]));

            Assert.Equal([null, "a", "b"], changed.Select(m => m.Bucket));
            Assert.All(Latest(model), map => Assert.Equal(new ConfigVersion(1, 11), map.Version));
            Assert.Equal([3, 4], model.Cluster.Nodes.Skip(3).Select(n => n.Member));
            Assert.All(model.Cluster.Nodes.Skip(3), n => Assert.Equal(["kv"], n.Services));
            Assert.Equal(3, model.Bucket("a").BucketNodes.Count);
            Assert.Equal([Node3, Node4], model.Bucket("a").Nodes.Skip(3).Select(n => n.Hostname));
        }

        [Fact]
        public void BucketChanges_BumpOnlyThatBucket()
        {
            var model = ClusterModel.Create(PrivateLink, buckets: ["a", "b"]);
            model.Apply(new AddNodes(1, ["kv"]));

            Assert.Equal(["a"], model.Apply(new AddToBucket("a", [Node3])).Select(m => m.Bucket));
            model.Apply(new StartRebalance("a", []));
            model.Apply(new MoveVBuckets("a", [model.PendingMoves("a")[0]]));

            Assert.Equal(new ConfigVersion(1, 5), model.Bucket("a").Version);
            Assert.Equal(new ConfigVersion(1, 2), model.Bucket("b").Version);
            Assert.Equal(new ConfigVersion(1, 2), model.Cluster.Version);
            Assert.Equal(5, model.History("a").Count);
            Assert.Equal(2, model.History(null).Count);
        }

        [Fact]
        public void BumpRev_ChangesOnlyRevOfOneMap()
        {
            var model = ClusterModel.Create(PrivateLink);
            var bucket = model.Profile.BucketName;
            var before = model.Bucket(bucket);

            Assert.Null(Assert.Single(model.Apply(new BumpRev(null))).Bucket);
            Assert.Equal(bucket, Assert.Single(model.Apply(new BumpRev(bucket))).Bucket);

            Assert.Equal(new ConfigVersion(1, 2), model.Cluster.Version);
            Assert.Equal(new ConfigVersion(1, 3), model.Bucket(bucket).Version);
            Assert.Equal(Leaves(model.Render(before, Node0).Rev(3).ToJson()), Leaves(model.Render(model.Bucket(bucket), Node0).ToJson()));
        }

        [Fact]
        public void ScaleOut_ThreeToFive_FollowsCapellaSequence()
        {
            var model = ClusterModel.Create(PrivateLink);
            var bucket = model.Profile.BucketName;

            Run(model, ScaleOut(model, bucket, 2), e =>
            {
                if (e is not AddToBucket) return;
                var map = model.Render(model.Bucket(bucket), null).Build().VBucketServerMap;
                Assert.Equal([Node3, Node4], map.ServerList.Skip(3).Select(s => s.Split(':')[0]));
                Assert.All(map.VBucketMap, chain => Assert.All(chain, copy => Assert.InRange(copy, 0, 2)));
            });

            var history = model.History(bucket);
            Assert.Equal(model.Applied.Count + 1, history.Count);
            // Only AddNodes and the 4 service changes touch the cluster map, so its rev jumps over the rebalance.
            var clusterRevs = model.History(null).Select(m => m.Version.Revision).ToList();
            Assert.Equal(6, clusterRevs.Count);
            Assert.True(clusterRevs[2] - clusterRevs[1] > 200, $"The cluster revs are {string.Join(", ", clusterRevs)}.");
            Assert.All(history, map =>
            {
                var config = model.Render(map, null).Build();
                Assert.Equal(map.Version, config.ConfigVersion);
                AssertChains(config.VBucketServerMap.VBucketMap, map.BucketNodes.Count);
                if (map.ForwardMap is not null) AssertChains(config.VBucketServerMap.VBucketMapForward, map.BucketNodes.Count);
            });

            var end = model.Bucket(bucket).Chains;
            AssertWithinOne(CopiesPerServer(end, 5, chain => chain.Take(1)));
            AssertWithinOne(CopiesPerServer(end, 5, chain => chain.Skip(1)));
            Assert.All(model.Cluster.Nodes, n => Assert.Equal(model.DefaultServices, n.Services));
        }

        [Fact]
        public void ScaleIn_FiveToFour_ListsLeavingNodeLastThenDropsIt()
        {
            var model = ClusterModel.Create(PrivateLink, nodes: 5);
            var bucket = model.Profile.BucketName;

            Run(model, ScaleIn(model, bucket, Node3), e =>
            {
                if (e is not RemoveFromBucket) return;
                var config = model.Render(model.Bucket(bucket), Node0).Build();
                Assert.Equal(Node3, config.NodesExt.Last().Hostname);
                Assert.DoesNotContain(config.VBucketServerMap.ServerList, s => s.StartsWith(Node3, StringComparison.Ordinal));
                Assert.DoesNotContain(config.Nodes, n => n.Hostname.StartsWith(Node3, StringComparison.Ordinal));
            });

            Assert.Null(model.Cluster.Node(Node3));
            foreach (var map in Latest(model))
            {
                var config = model.Render(map, Node3).Build();
                Assert.DoesNotContain(config.NodesExt, n => n.ThisNode);
                Assert.DoesNotContain(config.NodesExt, n => n.Hostname == Node3);
            }
        }

        [Fact]
        public void Render_ForEachHost_MarksOnlyThatHost()
        {
            var model = ClusterModel.Create(CapellaPublic, nodes: 4);
            model.Apply(new AddNodes(1, ["kv"]));

            foreach (var map in Latest(model))
            {
                foreach (var host in model.Cluster.Nodes.Select(n => n.Hostname))
                {
                    var config = model.Render(map, host).Build();
                    Assert.Equal([host], config.NodesExt.Where(n => n.ThisNode).Select(n => n.Hostname));
                }
                Assert.DoesNotContain(model.Render(map, null).Build().NodesExt, n => n.ThisNode);
            }
        }

        [Fact]
        public void MoveVBuckets_NotPending_Throws()
        {
            var model = ClusterModel.Create(PrivateLink);
            var bucket = model.Profile.BucketName;
            model.Apply(new AddNodes(1, ["kv"]));
            model.Apply(new AddToBucket(bucket, [Node3]));
            model.Apply(new StartRebalance(bucket, []));
            var pending = model.PendingMoves(bucket);
            var settled = Enumerable.Range(0, 1024).First(v => !pending.Contains(v));

            AssertRejected(model, new MoveVBuckets(bucket, [pending[0], settled]), $"vbucket {settled} is not pending");
            Assert.Equal(pending, model.PendingMoves(bucket));
        }

        [Fact]
        public void FinishRebalance_WithMovesPending_Throws()
        {
            var model = ClusterModel.Create(PrivateLink);
            var bucket = model.Profile.BucketName;
            model.Apply(new AddNodes(1, ["kv"]));
            model.Apply(new AddToBucket(bucket, [Node3]));
            model.Apply(new StartRebalance(bucket, []));

            AssertRejected(model, new FinishRebalance(bucket), "vbuckets are pending");
            AssertRejected(model, new StartRebalance(bucket, []), "A rebalance is running");
        }

        [Fact]
        public void RemoveFromBucket_HostWithCopies_Throws()
        {
            var model = ClusterModel.Create(PrivateLink);
            var bucket = model.Profile.BucketName;

            AssertRejected(model, new RemoveFromBucket(bucket, [Node0]), "holds a copy of vbucket");

            model.Apply(new StartRebalance(bucket, [Node0]));
            model.Apply(new MoveVBuckets(bucket, model.PendingMoves(bucket)));
            AssertRejected(model, new EjectNodes([Node0]), $"{Node0} is in bucket {bucket}");
        }

        [Fact]
        public void AddToBucket_NodeWithoutKv_Throws()
        {
            var model = ClusterModel.Create(PrivateLink);
            model.Apply(new AddNodes(1, ["n1ql"]));

            AssertRejected(model, new AddToBucket(model.Profile.BucketName, [Node3]), $"{Node3} has no kv");
            AssertRejected(model, new SetServices(Node0, ["n1ql"]), "so it keeps kv");
            AssertRejected(model, new AddNodes(1, ["fts"]), "has no service fts");
        }

        [Fact]
        public void RejoinNode_HostInNodesExt_Throws()
        {
            var model = ClusterModel.Create(PrivateLink);

            AssertRejected(model, new RejoinNode(Node0), $"{Node0} is in nodesExt");
            AssertRejected(model, new RejoinNode("node-9.example.com"), "was never in the cluster");
        }

        [Fact]
        public void RejoinNode_EjectedHost_IsNewMemberWithSameHostname()
        {
            var profile = PrivateLink;
            var model = ClusterModel.Create(profile, nodes: 4);
            var bucket = model.Profile.BucketName;
            Run(model, ScaleIn(model, bucket, Node3));

            model.Apply(new RejoinNode(Node3, ["kv"]));

            var node = model.Cluster.Nodes.Last();
            Assert.Equal(Node3, node.Hostname);
            Assert.Equal(4, node.Member);
            Assert.Equal(profile.NodeShape.NodeUuid(4), node.NodeUuid);
            Assert.NotEqual(profile.NodeShape.NodeUuid(3), node.NodeUuid);
            var rendered = model.Render(model.Cluster, Node3).Build().NodesExt.Single(n => n.Hostname == Node3);
            Assert.Equal(node.NodeUuid, rendered.NodeUuid);
            Assert.True(rendered.ThisNode);

            model.Apply(new AddNodes(1));
            Assert.Equal("node-5.example.com", model.Cluster.Nodes.Last().Hostname);
        }

        [Fact]
        public void SameSeedAndEvents_GiveSameJsonForEveryVersion()
        {
            var first = ScaledOut(seed: 7);
            var second = ScaledOut(seed: 7);

            foreach (var bucket in new string?[] { null, first.Profile.BucketName })
            {
                var a = first.History(bucket);
                var b = second.History(bucket);
                Assert.Equal(a.Count, b.Count);
                for (var i = 0; i < a.Count; i++)
                {
                    Assert.Equal(first.Render(a[i], Node3).ToJson(), second.Render(b[i], Node3).ToJson());
                }
            }
            Assert.Equal(first.Trace(), second.Trace());

            var other = ClusterModel.Create(PrivateLink, seed: 8);
            Assert.NotEqual(first.Render(first.History(first.Profile.BucketName)[0], null).ToJson(),
                other.Render(other.Bucket(other.Profile.BucketName), null).ToJson());
        }

        [Fact]
        public void Versions_ShareChainsThatDidNotMove()
        {
            var model = ClusterModel.Create(PrivateLink);
            var bucket = model.Profile.BucketName;
            model.Apply(new AddNodes(1, ["kv"]));
            model.Apply(new AddToBucket(bucket, [Node3]));
            model.Apply(new StartRebalance(bucket, []));
            var moved = model.PendingMoves(bucket)[0];
            var still = model.PendingMoves(bucket)[1];
            var before = model.Bucket(bucket);

            model.Apply(new MoveVBuckets(bucket, [moved]));

            var after = model.Bucket(bucket);
            Assert.Same(before.VBucketMap[still], after.VBucketMap[still]);
            Assert.Same(before.ForwardMap![moved], after.VBucketMap[moved]);
            Assert.Equal(before.ForwardChain(moved), after.Chain(moved));
            Assert.Equal(after.Chain(moved)[0], after.ActiveHost(moved));
            Assert.DoesNotContain(moved, model.PendingMoves(bucket));
        }

        [Fact]
        public void Trace_ListsEachEventWithRevAndMaps()
        {
            var model = ClusterModel.Create(PrivateLink, rev: 5);
            var bucket = model.Profile.BucketName;

            model.Apply(new AddNodes(2, ["kv"]));
            model.Apply(new AddToBucket(bucket, [Node3, Node4]));
            model.Apply(new BumpRev(null));

            Assert.Equal(
                $"6 AddNodes 2 [kv] -> cluster, {bucket}\n7 AddToBucket {bucket} [{Node3}, {Node4}] -> {bucket}\n8 BumpRev cluster -> cluster",
                model.Trace());
            Assert.Equal([6UL, 7UL, 8UL], model.Applied.Select(a => a.Rev));
        }

        [Fact]
        public void ModelServer_DeliversLatestVersionPerHost()
        {
            var model = ClusterModel.Create(PrivateLink, nodes: 4);
            var bucket = model.Profile.BucketName;
            var server = new ModelServer(model, new FakeClusterServer());
            server.Seed();
            Run(model, ScaleIn(model, bucket, Node3));

            server.Deliver(Node0);
            Assert.Equal(model.Bucket(bucket).Version, server.Served(Node0, bucket));
            Assert.Equal(new ConfigVersion(1, 1), server.Served("node-1.example.com", bucket));

            server.DeliverAll();
            Assert.All(model.Cluster.Nodes, n => Assert.Equal(model.Cluster.Version, server.Served(n.Hostname, null)));
            // The ejected host learns that it left.
            Assert.Equal(model.Cluster.Version, server.Served(Node3, null));
            Assert.Equal(model.Bucket(bucket).Version, server.Served(Node3, bucket));

            model.Apply(new BumpRev(null));
            server.DeliverAll();
            Assert.NotEqual(model.Cluster.Version, server.Served(Node3, null));
        }

        /// <summary>
        /// The SDK follows a scale out that the model drives, one event at a time. Every node notifies its connections of
        /// each new version.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task ScaleOut_WithSdk_EndsOnModelMaps(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic).Connections(mode).StartAsync();

            await scenario.ScaleOut(2).RunAsync();

            var latest = scenario.Model.Bucket(scenario.Model.Profile.BucketName);
            var view = scenario.View();
            Assert.Equal(latest.Version, view.Version);
            Assert.Equal(latest.BucketNodes.OrderBy(h => h, StringComparer.Ordinal), view.Nodes);
            Assert.Empty(ScenarioCheck.WrongActives(view, latest));
        }

        /// <summary>
        /// Like the public Capella capture, the first node leaves. It is the node the SDK bootstrapped from.
        /// </summary>
        [Theory]
        [InlineData(ConnectionMode.Fake)]
        [InlineData(ConnectionMode.Real)]
        public async Task ScaleIn_WithSdk_FollowsEveryVersion(ConnectionMode mode)
        {
            await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic, nodes: 4).Connections(mode).StartAsync();

            await scenario.ScaleIn(Node0).RunAsync();

            Assert.DoesNotContain(Node0, scenario.View().Nodes);
        }

        private static ClusterProfile PrivateLink => ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);

        private static ClusterProfile CapellaPublic => ClusterProfile.Load(ClusterProfile.CapellaPublic);

        internal static ClusterProfile Docker => ClusterProfile.Load(ClusterProfile.Docker);

        private static ClusterModel ScaledOut(int seed)
        {
            var model = ClusterModel.Create(PrivateLink, seed: seed);
            Run(model, ScaleOut(model, model.Profile.BucketName, 2, seed));
            return model;
        }

        internal static IEnumerable<ModelMap> Latest(ClusterModel model) => model.Buckets.Select(model.Bucket).Prepend(model.Cluster);

        /// <summary>
        /// The render for the host the example came from has the leaves of the example, except rev and the vbucket entries.
        /// </summary>
        internal static void AssertMatches(ClusterModel model, ModelMap map, string example, string? servingHost = null)
        {
            var expected = model.Profile.Example(example);
            servingHost ??= ThisNode(expected.Json);
            Assert.Equal(WithoutRev(expected.Json), WithoutRev(model.Render(map, servingHost).ToJson()));
        }

        private static List<string> WithoutRev(string json) =>
            Leaves(json).Where(leaf => !leaf.StartsWith("rev ", StringComparison.Ordinal)).ToList();

        private static string? ThisNode(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("nodesExt").EnumerateArray()
                .Where(n => n.TryGetProperty("thisNode", out var flag) && flag.GetBoolean())
                .Select(n => n.GetProperty("hostname").GetString())
                .FirstOrDefault();
        }

        /// <summary>
        /// The event throws, and the model has the same versions, events and JSON as before.
        /// </summary>
        internal static void AssertRejected(ClusterModel model, ModelEvent e, string reason)
        {
            var applied = model.Applied.Count;
            var before = Latest(model).Select(m => model.Render(m, Node0).ToJson()).ToList();

            var error = Assert.Throws<InvalidOperationException>(() => model.Apply(e));

            Assert.Contains(e.ToString(), error.Message);
            Assert.Contains(reason, error.Message);
            Assert.Equal(applied, model.Applied.Count);
            Assert.Equal(before, Latest(model).Select(m => model.Render(m, Node0).ToJson()));
        }

        private static void AssertChains(short[][] chains, int servers) =>
            Assert.All(chains, chain =>
            {
                Assert.All(chain, copy => Assert.InRange(copy, (short)-1, (short)(servers - 1)));
                Assert.Equal(chain.Count(c => c >= 0), chain.Where(c => c >= 0).Distinct().Count());
            });

        internal static void AssertWithinOne(IReadOnlyList<int> counts) =>
            Assert.True(counts.Max() - counts.Min() <= 1, $"The counts {string.Join(", ", counts)} differ by more than 1.");
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
