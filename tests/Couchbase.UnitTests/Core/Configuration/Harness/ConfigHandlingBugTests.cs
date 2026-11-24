#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Configuration.Model;
using Couchbase.Test.Common.Configuration.Scenarios;
using Couchbase.Test.Common.Utils;
using Xunit;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigHarnessTests;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigPushTests;
using static Couchbase.UnitTests.Core.Configuration.Harness.RealConnectionTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// Each test asserts the correct behaviour for a known config handling bug, so it fails until the bug is fixed.
    /// </summary>
    public class ConfigHandlingBugTests
    {
        private const string OtherBucket = "inventory";

        /// <summary>
        /// The real time a bucket gets to apply a pushed map while one of its nodes hangs. Two passes can each wait out
        /// the shortest config fetch timeout on the hung node.
        /// </summary>
        private static readonly TimeSpan HungNodeDeadline = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Gives the shortest config fetch timeout, which is 1 s.
        /// </summary>
        private static readonly TimeSpan FastestPollInterval = TimeSpan.FromSeconds(1);

        // Bug 1
        [Fact]
        public async Task NewerGlobalMap_WithSameHosts_ReplacesGlobalConfig()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            await harness.ApplyGlobalAsync(GlobalMap(20).Node("node-0.example.com", n => n.Query(9093)).Build());

            Assert.Equal(Rev(20), harness.GlobalVersion);
        }

        // Bug 1
        [Fact]
        public async Task OlderGlobalMap_AfterNewerOne_LeavesNodesAtNewerRev()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            Assert.Equal([BucketName, BucketName, null], harness.Nodes.Select(n => n.Owner));

            await harness.ApplyGlobalAsync(GlobalMap(20).Node("node-0.example.com", n => n.Query(9093)).Build());
            await harness.ApplyGlobalAsync(GlobalMap(15).Build());

            Assert.All(harness.Nodes, n => Assert.Equal(Rev(20), n.Version));
            Assert.Equal(9093, harness.Nodes[0].QueryUri!.Port);
        }

        // Bug 1
        [Fact]
        public async Task OlderGlobalMap_AfterNewerOneRemovedHost_DoesNotRecreateIt()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.ApplyGlobalAsync(GlobalMap(20).RemoveNode(QueryNode).Build());

            await harness.ApplyGlobalAsync(GlobalMap(15).Build());

            Assert.DoesNotContain(QueryNode, harness.Nodes.Select(n => n.Hostname));
            Assert.Equal(3, harness.NodesCreated);
        }

        // Bug 1
        [Fact]
        public async Task NewerGlobalMap_WithNewClusterCapability_IsVisibleOnGlobalConfig()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            await harness.ApplyGlobalAsync(GlobalMap(20).ClusterCapabilities("n1ql", "enhancedPreparedStatements").Build());

            Assert.True(harness.Context.GlobalConfig.HasClusterCap("n1ql.enhancedPreparedStatements"),
                "GlobalConfig does not have the n1ql.enhancedPreparedStatements capability of rev 20.");
        }

        // Bug 1
        [Fact]
        public async Task Polling_WithLaggingNode_LeavesNodesAtNewestRev()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), pollInterval: PollInterval);
            harness.Server.SetMap(GlobalMap(20).Node(Node0, n => n.Query(9093)), Node0, Node1);
            await harness.StartPollingAsync();
            await harness.AdvanceAsync(PollInterval);

            // Node-1 now lags on rev 15 and node-0 fails, so the next poll gets rev 15 from node-1 after rev 20.
            harness.Server.SetMap(GlobalMap(15), Node1);
            harness.Server.FailFetchesAfter(0, Node0);
            await harness.AdvanceAsync(PollInterval);

            Assert.All(harness.Nodes, n => Assert.Equal(Rev(20), n.Version));
            Assert.Equal(9093, harness.Nodes[0].QueryUri!.Port);
        }

        // Bug 3
        [Fact]
        public async Task NotMyVBucketBody_WithHostPlaceholder_UsesNodeHost()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(ClusterMapBuilder.Bucket(BucketName).Rev(10).Nodes(2, n => n.Kv().Query()));
            var key = Enumerable.Range(0, 100).Select(i => $"key-{i}")
                .First(k => bucket.KeyMapper!.MapKey(k).LocatePrimary()?.Host == Node0);

            // Node-0 names itself $HOST in the body, like a server with no hostname set.
            harness.Server.NotMyVBucket(Node0, ClusterMapBuilder.Bucket(BucketName).Rev(11)
                .Node(NodeBuilder.HostPlaceholder, n => n.Uuid("node-uuid-0").Kv().Query())
                .Node(Node1, n => n.Uuid("node-uuid-1").Kv().Query())
                .VBuckets(1024, replicas: 2));
            // Only the not-my-vbucket reply matters, so the Get stops once its config is published.
            // Its retries wait on the harness clock, which does not move here.
            using var cts = new System.Threading.CancellationTokenSource();
            var get = Record.ExceptionAsync(() => bucket.DefaultCollection().GetAsync(key, o => o.CancellationToken(cts.Token)));
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            cts.Cancel();
            await get;
            await harness.DrainAsync();

            // The SDK turns a hostname that still holds $HOST into localhost, so check every host.
            Assert.Equal([Node0, Node1, QueryNode], harness.Nodes.Select(n => n.Hostname));
            Assert.Equal([Node0, Node1], harness.Bucket(BucketName).Nodes);
            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.Equal([$"{Node0}:11210", $"{Node1}:11210"], bucket.CurrentConfig!.VBucketServerMap.ServerList);
        }

        // Bug 4
        [Fact]
        public async Task BucketMap_WithIgnoreRevAndSameRev_IsApplied()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());

            // A DNS SRV rebootstrap can reach a cluster whose map has the same rev.
            var config = BucketMap().VBuckets(1024, replicas: 2).Build();
            config.IgnoreRev = true;
            await harness.ApplyBucketAsync(BucketName, config);

            Assert.Equal(2, bucket.CurrentConfig!.VBucketServerMap.NumReplicas);
        }

        // Bug 5
        [Fact]
        public async Task BucketMap_AfterProcessingFailedOnce_IsAppliedWhenPublishedAgain()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            var next = BucketMap(11).Node("node-3.example.com");

            harness.FailNodeCreation("node-3.example.com");
            harness.Publish(next);
            await harness.DrainAsync();
            Assert.Equal(Rev(10), harness.Bucket(BucketName).Version);

            harness.AllowNodeCreation("node-3.example.com");
            harness.Publish(next);
            await harness.DrainAsync();

            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.Contains("node-3.example.com", harness.Bucket(BucketName).Nodes);
        }

        // Bug 5
        [Fact]
        public async Task BucketMap_WithNodeThatStaysUnreachable_AppliesTheNextMapWithoutIt()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.FailNodeCreation("node-3.example.com");

            for (var i = 0; i < 3; i++)
            {
                harness.Publish(BucketMap(11).Node("node-3.example.com"));
                await harness.DrainAsync();
                Assert.Equal(Rev(10), harness.Bucket(BucketName).Version);
                Assert.Equal(i + 1, harness.NodeCreationAttempts("node-3.example.com"));
            }

            harness.Publish(BucketMap(12).VBuckets(1024, replicas: 2));
            await harness.DrainAsync();

            var view = harness.Bucket(BucketName);
            Assert.Equal(Rev(12), view.Version);
            Assert.Equal(["node-0.example.com", "node-1.example.com"], view.Nodes);
            Assert.Equal(2, bucket.CurrentConfig!.VBucketServerMap.NumReplicas);
            Assert.Equal(3, harness.NodesCreated);
        }

        // Bug 6
        [Fact]
        public async Task BucketMap_WithOnlyNewRev_BecomesCurrentConfig()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());

            harness.Publish(BucketMap(11));
            await harness.DrainAsync();

            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
        }

        // Bug 6
        [Fact]
        public async Task BucketMap_WithOnlyNewBucketCapability_BecomesCurrentConfig()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());

            harness.Publish(BucketMap(11).BucketCapabilities("rangeScan"));
            await harness.DrainAsync();

            Assert.True(bucket.CurrentConfig!.HasBucketCap("rangeScan"),
                $"The bucket config at rev {bucket.CurrentConfig.ConfigVersion} does not have the rangeScan capability of rev 11.");
        }

        // Bug 10
        [Fact]
        public async Task ExternalNetwork_BucketMapWithNodeMissingAlternateAddress_IsApplied()
        {
            await using var harness = ConfigHarness.Create(External(ClusterMapBuilder.Global().Rev(10)),
                options => options.NetworkResolution = NetworkResolution.External);
            await harness.OpenBucketAsync(External(ClusterMapBuilder.Bucket(BucketName).Rev(10)));

            // A node added on Kubernetes can show up before its alternate address does.
            harness.Publish(External(ClusterMapBuilder.Bucket(BucketName).Rev(11)).Node("node-2.example.com"));
            await harness.DrainAsync();

            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.All(harness.Nodes.Where(n => n.Hostname.StartsWith("203.0.113.")),
                n => Assert.Equal(Rev(11), n.Version));
        }

        // Bug 11
        [Fact]
        public async Task BucketMap_WithOnlyServerGroupChange_UpdatesServerGroups()
        {
            var map = BucketMap()
                .Node("node-0.example.com", n => n.ServerGroup("group-1"))
                .Node("node-1.example.com", n => n.ServerGroup("group-2"));
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(map);
            Assert.Equal(["node-1.example.com:11210"], harness.Bucket(BucketName).ServerGroups["group-2"]);

            harness.Publish(map.Clone().Rev(11).Node("node-1.example.com", n => n.ServerGroup("group-1")));
            await harness.DrainAsync();

            Assert.Equal(["node-0.example.com:11210", "node-1.example.com:11210"],
                harness.Bucket(BucketName).ServerGroups["group-1"]);
        }

        // Bug 12
        [Fact]
        public async Task ExternalNetwork_ServerGroupsAfterBucketMapApplied_MapToNodes()
        {
            await using var harness = ConfigHarness.Create(External(ClusterMapBuilder.Global().Rev(10)),
                options => options.NetworkResolution = NetworkResolution.External);
            var bucket = await harness.OpenBucketAsync(WithServerGroups(External(ClusterMapBuilder.Bucket(BucketName).Rev(10))));
            harness.Server.SetMap(WithServerGroups(External(ClusterMapBuilder.Bucket(BucketName).Rev(11)).VBuckets(1024, replicas: 0)));

            harness.Server.PushBrief(Node0, 1, 11);
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            await harness.DrainAsync();

            Assert.Equal(Rev(11), bucket.CurrentConfig!.ConfigVersion);
            Assert.Null(Record.Exception(() => bucket.CurrentConfig.ServerGroupNodeIndexes));
            var groups = harness.Bucket(BucketName).ServerGroups;
            Assert.Equal(["203.0.113.10:32210"], groups["group-1"]);
            Assert.Equal(["203.0.113.11:32210"], groups["group-2"]);
        }

        // Bug 17
        [Theory]
        [InlineData(NetworkResolution.External)]
        [InlineData(NetworkResolution.Auto)]
        public async Task NewerGlobalMap_WithNewFirstNodeWithoutAlternateAddress_KeepsExternalNetwork(string network)
        {
            await using var harness = ConfigHarness.Create(
                WithAlternateAddresses(ClusterMapBuilder.Global().Rev(10), 1, 2),
                options => options.NetworkResolution = network);
            Assert.Equal(NetworkResolution.External, harness.Options.EffectiveNetworkResolution);

            var next = ClusterMapBuilder.Global().Rev(11).Node("node-0.example.com", n => n.Uuid("node-uuid-0"));
            harness.Publish(WithAlternateAddresses(next, 1, 2));
            await harness.DrainAsync();

            Assert.Equal(NetworkResolution.External, harness.Options.EffectiveNetworkResolution);
        }

        // Bug 17
        [Theory]
        [InlineData(NetworkResolution.External)]
        [InlineData(NetworkResolution.Auto)]
        public async Task NewerGlobalMap_WhereFirstNodeLosesAlternateAddress_KeepsExternalNetwork(string network)
        {
            await using var harness = ConfigHarness.Create(
                WithAlternateAddresses(ClusterMapBuilder.Global().Rev(10), 0, 1),
                options => options.NetworkResolution = network);
            Assert.Equal(NetworkResolution.External, harness.Options.EffectiveNetworkResolution);

            var next = ClusterMapBuilder.Global().Rev(11).Node("node-0.example.com", n => n.Uuid("node-uuid-0"));
            harness.Publish(WithAlternateAddresses(next, 1));
            await harness.DrainAsync();

            Assert.Equal(NetworkResolution.External, harness.Options.EffectiveNetworkResolution);
        }

        // Bug 18
        [Fact]
        public async Task BucketMap_WithDataNodeOutsideBucket_MapsServerGroupsToBucketNodes()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(WithServerGroups(BucketMap()));

            // The node joined the cluster but not yet the bucket, like at the start of a Capella scale out.
            harness.Publish(WithServerGroups(BucketMap(11))
                .Node("node-3.example.com", n => n.Uuid("node-uuid-3").Kv().ServerGroup("group-1").OutsideBucket()));
            await harness.DrainAsync();

            Assert.Equal(Rev(11), bucket.CurrentConfig!.ConfigVersion);
            Assert.Null(Record.Exception(() => bucket.CurrentConfig.ServerGroupNodeIndexes));
            var groups = harness.Bucket(BucketName).ServerGroups;
            Assert.Equal(["node-0.example.com:11210"], groups["group-1"]);
            Assert.Equal(["node-1.example.com:11210"], groups["group-2"]);
        }

        // Bug 19
        [Fact]
        public async Task BucketMap_WithForwardMap_GivesEachForwardVBucketOneEntryPerReplica()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            var bucket = await harness.OpenBucketAsync(BucketMap().ForwardMap());

            var forward = Assert.IsType<VBucketKeyMapper>(bucket.KeyMapper).GetVBucketsForwards();
            Assert.NotEmpty(forward);
            Assert.Equal([1], forward.Values.Select(v => ((VBucket)v).Replicas.Length).Distinct());
        }

        // Bug 20
        // The push handler fetches the map from a random bucket node. On node-1 the fetch waits for an in-flight slot
        // with no timeout, and the hung node keeps every slot, so the fetch never ends.
        // A config fetch times out after the poll interval, and at least 1 s.
        [Fact]
        public async Task BucketMap_PushedWhileANodeHangsWithFullSlots_IsApplied()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), options =>
            {
                options.KvTimeout = SmallKvTimeout;
                options.ConfigPollInterval = FastestPollInterval;
                options.Tuning.MaximumInFlightOperationsPerConnection = 1;
            }, clock: TimeProvider.System, mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.Server.Hang(Node1);
            foreach (var key in KeysOn(bucket, Node1).Take(4))
            {
                _ = bucket.DefaultCollection().GetAsync(key);
            }
            await harness.WaitUntilAsync(() => harness.Server.ConnectionsOn(Node1) is { Count: 2 } open && open.All(c => c.RepliesHeld == 1),
                Timeout, "Each node-1 connection holding one Get");

            // Node-0 has nothing newer for the first fetch, so the bucket refresher asks node-1 in either order.
            harness.Server.SetMapAfter(1, BucketMap(11).VBuckets(1024, replicas: 2), Node0);
            harness.Server.PushBrief(Node0, 1, 11);

            await harness.WaitUntilAsync(() => harness.Bucket(BucketName).Version == Rev(11), HungNodeDeadline,
                "The bucket taking rev 11");
        }

        // Bug 20
        // The fetch on the hung node-1 times out. The push handler then drops the pushed version and waits for a newer
        // push, and polling skips bucket nodes, so nothing fetches the map again.
        // A config fetch times out after the poll interval, and at least 1 s.
        [Fact]
        public async Task BucketMap_PushedWhileANodeHangs_IsApplied()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), options =>
            {
                options.KvTimeout = SmallKvTimeout;
                options.ConfigPollInterval = FastestPollInterval;
            }, clock: TimeProvider.System, mode: ConnectionMode.Real);
            await harness.OpenBucketAsync(BucketMap());
            harness.Server.Hang(Node1);

            // Node-0 has nothing newer for the first fetch, so the bucket refresher asks node-1 in either order.
            harness.Server.SetMapAfter(1, BucketMap(11).VBuckets(1024, replicas: 2), Node0);
            harness.Server.PushBrief(Node0, 1, 11);

            await harness.WaitUntilAsync(() => harness.Bucket(BucketName).Version == Rev(11), HungNodeDeadline,
                "The bucket taking rev 11");
        }

        // Bug 22
        // Polling skips a bucket connection that negotiated brief push, and once a bucket opens every kv node belongs to
        // it. So a map that comes with no push, or a push the handler dropped, never reaches the bucket.
        // The expected behaviour is the Java SDK's, which polls a bucket on its interval with push on. No RFC requires it.
        [Fact]
        public async Task BucketMap_ServedWithNoPush_IsFoundByPolling()
        {
            await using var scenario = await Scenario.On(ClusterProfile.Docker).StartAsync();

            await scenario.NoPush().FailOver("node-2", Failover.Hard).RunAsync();
        }

        // Bug 23
        // On Private Link every map node has the seed host. The bootstrap matches the map nodes to the seed node by host
        // only, so it creates no node for them and gives the seed node the adapter of each map node in turn.
        [Fact]
        public async Task PrivateLink_Bootstrap_CreatesAGlobalNodePerMapNode()
        {
            var model = ClusterModel.Create(PrivateLink, buckets: []);

            await using var harness = ConfigHarness.Create(model.Render(model.Cluster, null), ConnectThroughPrivateEndpoint,
                bootstrap: BootstrapMode.Sdk);

            AssertGlobalNodePerMapNode(harness, model.Cluster);
        }

        // Bug 23
        // A global map update finds the node of each map node by host only. On Private Link that is the first node for
        // every map node, so a new node gets no global node and the first node gets the adapter of the last map node.
        [Fact]
        public async Task PrivateLink_NewerGlobalMapWithNewNode_KeepsAGlobalNodePerMapNode()
        {
            var model = ClusterModel.Create(PrivateLink, buckets: []);
            await using var harness = ConfigHarness.Create(model.Render(model.Cluster, null), ConnectThroughPrivateEndpoint);

            model.Apply(new AddNodes(1));
            await PublishGlobalAsync(harness, model);

            AssertGlobalNodePerMapNode(harness, model.Cluster);
        }

        // Bug 23
        // The prune keeps a global node when a map node has its host. On Private Link every map node has it, so a node
        // that leaves the map keeps its global node.
        [Fact]
        public async Task PrivateLink_NewerGlobalMapWithoutANode_RemovesItsGlobalNode()
        {
            var model = ClusterModel.Create(PrivateLink, buckets: []);
            await using var harness = ConfigHarness.Create(model.Render(model.Cluster, null), ConnectThroughPrivateEndpoint);

            var leaving = model.Cluster.Nodes[^1].Hostname;
            model.Apply(new EjectNodes([leaving]));
            await PublishGlobalAsync(harness, model);

            // Only the names count, as the update also gives the adapters by host.
            var global = GlobalNodes(harness).Select(n => n.Node).ToList();
            Assert.True(global.SequenceEqual(MapNodes(model.Cluster)),
                $"{leaving} left the global map, but the global nodes are [{string.Join(", ", global)}].");
        }

        // Bug 24
        // Node-1 left the cluster. It closed its connections and every reconnect fails SELECT_BUCKET, so its pool has no
        // live connection. The push handler still picks it, and the fetch waits in a send queue that nothing reads until
        // it times out. The fetch timeout, 5 s for this poll interval, is longer than the deadline, so the bucket misses
        // the deadline.
        [Theory]
        [InlineData(ResponseStatus.Eaccess)]
        [InlineData(ResponseStatus.KeyNotFound)]
        public async Task BucketMap_PushedAfterANodeLeft_IsApplied(ResponseStatus selectBucketStatus)
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), options =>
            {
                options.KvTimeout = TimeSpan.FromSeconds(10);
                options.ConfigPollInterval = TimeSpan.FromSeconds(10);
            }, clock: TimeProvider.System, mode: ConnectionMode.Real);
            await harness.OpenBucketAsync(BucketMap());
            harness.Server.Leave(Node1, selectBucketStatus);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node1) is { SelectBucketsFailed: > 0, OpenConnections: 0 },
                Timeout, "Node-1 failing SELECT_BUCKET with no open connection");

            // Node-0 has nothing newer for the first fetch, so the bucket refresher asks node-1 in either order.
            harness.Server.SetMapAfter(1, BucketMap(11).VBuckets(1024, replicas: 2), Node0);
            harness.Server.PushBrief(Node0, 1, 11);

            await harness.WaitUntilAsync(() => harness.Bucket(BucketName).Version == Rev(11), HungNodeDeadline,
                "The bucket taking rev 11");
        }

        // NCBC-4390
        // A bucket keeps its config when an apply fails, so each poll publishes the same version again. When the apply
        // fails slowly, on a connect timeout to a new node, the copies queue up and a map of another bucket waits for
        // one retry per copy.
        [Fact]
        public async Task BucketMap_BehindASlowFailingApplyOfAnotherBucket_WaitsForOneRetryOnly()
        {
            const string node3 = "node-3.example.com";
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            await harness.OpenBucketAsync(BucketMap(OtherBucket));
            var withNode3 = BucketMap(11).Node(node3);
            harness.HoldNodeCreation(node3);
            try
            {
                harness.Publish(withNode3);
                await harness.NodeCreationHeldAsync(node3).WaitAsync(Timeout);
                for (var i = 0; i < 10; i++)
                {
                    harness.Publish(withNode3);
                }
                harness.Publish(BucketMap(OtherBucket, 11).VBuckets(1024, replicas: 2));

                harness.FailNodeCreation(node3);
                harness.ReleaseNodeCreation(node3);
                await harness.DrainAsync().WaitAsync(Timeout);
            }
            finally
            {
                harness.ReleaseNodeCreation(node3);
            }

            Assert.Equal(Rev(11), harness.Bucket(OtherBucket).Version);
            Assert.Equal(Rev(10), harness.Bucket(BucketName).Version);
            // The held attempt, then one retry for all the copies.
            Assert.Equal(2, harness.NodeCreationAttempts(node3));
        }

        // NCBC-4390
        // A DNS SRV rebootstrap runs this rebootstrap for each bucket. A cluster created again behind the same DNS name
        // starts at low revisions, and the bucket skipped each push at or below a version the old cluster pushed.
        [Fact]
        public async Task BriefPush_AfterARebootstrapToLowerRevisions_IsFetched()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            harness.Server.SetMap(BucketMap(100));
            harness.Server.PushBrief(Node0, 1, 100);
            await harness.SettleAsync();
            Assert.Equal(Rev(100), harness.Bucket(BucketName).Version);

            harness.Server.SetMap(BucketMap(5));
            await harness.Context.RebootStrapAsync(BucketName).WaitAsync(Timeout);
            Assert.Equal(Rev(5), harness.Bucket(BucketName).Version);

            harness.Server.SetMap(BucketMap(6));
            harness.Server.PushBrief(Node0, 1, 6);
            await harness.SettleAsync();

            Assert.Equal(Rev(6), harness.Bucket(BucketName).Version);
        }

        // NCBC-4417
        // Before 7.6.2 the server sends the map of a new bucket before it places the vbuckets, so for about a second
        // vBucketMap is empty (MB-60405). A DNS SRV rebootstrap forces the map on the bucket with IgnoreRev.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BucketMap_WithEmptyVBucketMap_IsDroppedUntilAMapArrives(bool ignoreRev)
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var keyMapper = bucket.KeyMapper;

            var empty = BucketMap(11).Node("node-3.example.com").VBuckets(0).Build();
            if (ignoreRev)
            {
                empty.IgnoreRev = true;
                await harness.ApplyBucketAsync(BucketName, empty).WaitAsync(Timeout);
            }
            else
            {
                harness.Publish(empty);
                await harness.DrainAsync().WaitAsync(Timeout);
            }

            Assert.Equal(Rev(10), harness.Bucket(BucketName).Version);
            Assert.Same(keyMapper, bucket.KeyMapper);
            Assert.Equal(0, harness.NodeCreationAttempts("node-3.example.com"));
            Assert.Equal(3, harness.NodesCreated);

            harness.Publish(BucketMap(12).Node("node-3.example.com"));
            await harness.DrainAsync().WaitAsync(Timeout);

            var view = harness.Bucket(BucketName);
            Assert.Equal(Rev(12), view.Version);
            Assert.Contains("node-3.example.com", view.Nodes);
        }

        // NCBC-4417
        // An open right after the bucket was created gets a config with no map from a server before 7.6.2 (MB-60405).
        // The open fetches the config again until the map arrives.
        [Fact]
        public async Task BucketOpen_WhileTheServerHasNoVBucketMap_WaitsForTheMap()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), bootstrap: BootstrapMode.Sdk);
            var open = harness.OpenBucketAsync(BucketMap().VBuckets(0));
            await harness.SettleAsync();
            Assert.False(open.IsCompleted, "The open did not wait for the vbucket map.");

            // The next fetch has no map either, and the one after has it.
            harness.Server.SetMapAfter(1, BucketMap(11), Node0);
            await harness.AdvanceAsync(TimeSpan.FromTicks(2 * harness.Options.ConfigPollFloorInterval.Ticks));

            await open.WaitAsync(Timeout);
            var view = harness.Bucket(BucketName);
            Assert.Equal(Rev(11), view.Version);
            Assert.Equal([Node0, Node1], view.Nodes);
            Assert.Equal(3, harness.Server.Fetches.Count(f => f.Bucket == BucketName));
        }

        // NCBC-4417
        // When no map arrives within KvTimeout the open fails. Each failed attempt must leave no node and no running
        // config refresher behind.
        [Fact]
        public async Task BucketOpen_WhenNoVBucketMapArrivesInKvTimeout_FailsAndLeavesNothingRunning()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), bootstrap: BootstrapMode.Sdk);
            var clock = (SteppableClock)harness.Clock;
            await harness.SettleAsync();
            var timers = clock.PendingTimers.Count;

            var open = harness.OpenBucketAsync(BucketMap().VBuckets(0));
            await harness.AdvanceAsync(harness.Options.KvTimeout);

            await Assert.ThrowsAsync<BucketNotFoundException>(() => open.WaitAsync(Timeout));
            Assert.DoesNotContain(harness.Nodes, n => n.Owner == BucketName);
            // A config refresher that still runs sleeps on the clock.
            Assert.Equal(timers, clock.PendingTimers.Count);
        }

        // NCBC-4424
        // The bucket updates its nodes only when the nodes or nodesExt compare changes, and that compare skips the
        // search and analytics ports. The global map is the same, so nothing else updates the node.
        [Theory]
        [InlineData(ServiceType.Search)]
        [InlineData(ServiceType.Analytics)]
        public async Task BucketMap_WithOnlyANewServiceOnANode_UpdatesTheNode(ServiceType service)
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());

            harness.Publish(WithService(BucketMap(11), Node0, service));
            await harness.DrainAsync().WaitAsync(Timeout);

            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            var node = harness.Nodes.Single(n => n.Name == Node0);
            Assert.NotNull(service == ServiceType.Search ? node.SearchUri : node.AnalyticsUri);
        }

        // NCBC-4424
        // On the external network the vbucket server list holds the alternate addresses, so a new alternate host moves
        // the vbuckets of node-1 to a new endpoint. The nodes compare skips alternate addresses, so the bucket keeps its
        // node at the old endpoint and an operation on those vbuckets finds no node.
        // The server routes the old and the new address to node-1, so the asserts check endpoints, not node names.
        [Fact]
        public async Task ExternalNetwork_BucketMapWithNewAlternateHost_MovesTheBucketNode()
        {
            await using var harness = ConfigHarness.Create(External(ClusterMapBuilder.Global().Rev(10)),
                options => options.NetworkResolution = NetworkResolution.External);
            var bucket = await harness.OpenBucketAsync(External(ClusterMapBuilder.Bucket(BucketName).Rev(10)));
            var oldEndPoint = new HostEndpointWithPort("203.0.113.11", 32210);
            var newEndPoint = new HostEndpointWithPort("203.0.113.21", 32210);
            Assert.Contains(oldEndPoint, BucketEndPoints(harness));

            var next = External(ClusterMapBuilder.Bucket(BucketName).Rev(11)).Node(Node1, n => n
                .AlternateAddress("external", newEndPoint.Host, p => p.Mgmt(32091, 32191).Kv(32210, 32207)));
            harness.Server.SetMap(next);
            harness.Publish(next);
            await harness.DrainAsync().WaitAsync(Timeout);

            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            var endPoints = BucketEndPoints(harness);
            Assert.Contains(newEndPoint, endPoints);
            Assert.DoesNotContain(oldEndPoint, endPoints);
            Assert.Equal(endPoints, bucket.Nodes.Select(n => n.EndPoint).OrderBy(e => e.ToString(), StringComparer.Ordinal));

            // Each vbucket's active endpoint must find a node, as an operation looks it up.
            var keyMapper = Assert.IsType<VBucketKeyMapper>(bucket.KeyMapper);
            var unreachable = Enumerable.Range(0, 1024)
                .Select(v => keyMapper[(short)v].LocatePrimary())
                .Where(e => e is null || !bucket.Nodes.TryGet(e.Value, out _))
                .Distinct()
                .ToList();
            Assert.Empty(unreachable);
        }

        // NCBC-4424
        // A bucket open holds the cluster config lock while it connects the nodes of its map. A map of another bucket
        // that needs no new node must not wait for that lock.
        [Fact]
        public async Task BucketMap_NeedingNoNewNode_IsAppliedWhileAnotherBucketOpenConnectsANode()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            // The other bucket connects node-0 first, then node-1 under the lock, as this bucket owns both.
            harness.HoldNodeCreation(Node1);
            try
            {
                var open = harness.OpenBucketAsync(BucketMap(OtherBucket));
                await harness.NodeCreationHeldAsync(Node1).WaitAsync(Timeout);

                harness.Publish(BucketMap(11).VBuckets(1024, replicas: 2));
                await harness.DrainAsync().WaitAsync(Timeout);
                Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);

                harness.Publish(WithService(BucketMap(12).VBuckets(1024, replicas: 2), Node0, ServiceType.Search));
                await harness.DrainAsync().WaitAsync(Timeout);
                Assert.Equal(Rev(12), harness.Bucket(BucketName).Version);
                Assert.NotNull(harness.Nodes.Single(n => n.Name == Node0 && n.Owner == BucketName).SearchUri);
                Assert.False(open.IsCompleted);

                harness.ReleaseNodeCreation(Node1);
                await open.WaitAsync(Timeout);
            }
            finally
            {
                harness.ReleaseNodeCreation(Node1);
            }
        }

        // NCBC-4424
        // Revisions count for the whole cluster, so a bucket map can be older than the cluster map the nodes took and
        // still newer than the map of the bucket. The bucket takes it, but its nodes must keep the newer ports.
        [Fact]
        public async Task OlderBucketMap_AfterANewerGlobalMapMovedPorts_KeepsTheNewerNodeView()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            harness.Publish(GlobalMap(20).Node(Node0, n => n.Query(9093)).Node(QueryNode, n => n.Query(9094)));
            await harness.DrainAsync().WaitAsync(Timeout);

            harness.Publish(BucketMap(15));
            await harness.DrainAsync().WaitAsync(Timeout);

            Assert.Equal(Rev(15), harness.Bucket(BucketName).Version);
            Assert.All(harness.Nodes, n => Assert.Equal(Rev(20), n.Version));
            Assert.Equal(9093, harness.Nodes.Single(n => n.Name == Node0).QueryUri!.Port);
            Assert.Equal(9094, harness.Nodes.Single(n => n.Name == QueryNode).QueryUri!.Port);
        }

        // NCBC-4423
        // A bucket prunes only the nodes it owns, and a node with no KV service has no owner. Once the bucket owns every
        // KV node no cluster map arrives, so nothing removes the query node.
        [Fact]
        public async Task NCBC4423_BucketMapWithoutANodeWithNoKv_RemovesTheNode()
        {
            await using var harness = ConfigHarness.Create(WithQueryOnlyNode(ThreeKvNodes(ClusterMapBuilder.Global(), 10)));
            await harness.OpenBucketAsync(WithQueryOnlyNode(ThreeKvNodes(ClusterMapBuilder.Bucket(BucketName), 10))).WaitAsync(Timeout);
            Assert.Null(harness.Nodes.Single(n => n.Name == Node3).Owner);

            harness.Server.SetMap(ThreeKvNodes(ClusterMapBuilder.Global(), 11));
            harness.Server.SetMap(ThreeKvNodes(ClusterMapBuilder.Bucket(BucketName), 11));
            harness.Publish(ThreeKvNodes(ClusterMapBuilder.Bucket(BucketName), 11));
            await harness.DrainAsync().WaitAsync(Timeout);

            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.True(harness.Nodes.All(n => n.Name != Node3),
                $"{Node3} left the bucket map, but the context has [{string.Join(", ", harness.Nodes)}].");
            var picked = Enumerable.Range(0, 200)
                .Select(_ => harness.Server.NodeAt(harness.Context.GetRandomNodeForService(ServiceType.Query).EndPoint))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            Assert.True(!picked.Contains(Node3), $"Query picked [{string.Join(", ", picked)}] after {Node3} left.");
        }

        // NCBC-4423
        // The open takes a cluster node for each endpoint it tries. When the select fails, it removes and disposes that node.
        [Fact]
        public async Task NCBC4423_BucketOpenRefusedOnEveryHost_KeepsTheClusterNodes()
        {
            await using var harness = ConfigHarness.Create(ThreeKvNodes(ClusterMapBuilder.Global(), 10), bootstrap: BootstrapMode.Sdk);
            AssertClusterNodesKept(harness);
            harness.Server.RefuseBucket(BucketName, ResponseStatus.Eaccess);

            await Assert.ThrowsAnyAsync<Exception>(() => harness.OpenBucketAsync(ThreeKvNodes(ClusterMapBuilder.Bucket(BucketName), 10)).WaitAsync(Timeout));

            AssertClusterNodesKept(harness);
        }

        // NCBC-4423
        [Fact]
        public async Task NCBC4423_BucketOpenWithNoVBucketMapInKvTimeout_KeepsTheClusterNodes()
        {
            await using var harness = ConfigHarness.Create(ThreeKvNodes(ClusterMapBuilder.Global(), 10), bootstrap: BootstrapMode.Sdk);
            await harness.SettleAsync();
            AssertClusterNodesKept(harness);

            var open = harness.OpenBucketAsync(ThreeKvNodes(ClusterMapBuilder.Bucket(BucketName), 10).VBuckets(0));
            await harness.AdvanceAsync(harness.Options.KvTimeout);

            await Assert.ThrowsAsync<BucketNotFoundException>(() => open.WaitAsync(Timeout));
            AssertClusterNodesKept(harness);
        }

        // NCBC-4423
        // The bucket map has a KV node that joined after the cluster bootstrap. Its creation fails, so the bucket does not
        // bootstrap and its Dispose removes the nodes it claimed.
        [Fact]
        public async Task NCBC4423_BucketOpenWhereANewNodeFails_KeepsTheClusterNodes()
        {
            await using var harness = ConfigHarness.Create(ThreeKvNodes(ClusterMapBuilder.Global(), 10), bootstrap: BootstrapMode.Sdk);
            AssertClusterNodesKept(harness);
            harness.FailNodeCreation(Node3);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                harness.OpenBucketAsync(ThreeKvNodes(ClusterMapBuilder.Bucket(BucketName), 10).Node(Node3)).WaitAsync(Timeout));

            AssertClusterNodesKept(harness);
        }

        // NCBC-4423
        // The bucket takes over the cluster nodes, and its Dispose removes every node it owns.
        [Fact]
        public async Task NCBC4423_BucketClosedWhileClusterStaysOpen_KeepsTheClusterNodes()
        {
            await using var harness = ConfigHarness.Create(ThreeKvNodes(ClusterMapBuilder.Global(), 10), bootstrap: BootstrapMode.Sdk);
            var bucket = await harness.OpenBucketAsync(ThreeKvNodes(ClusterMapBuilder.Bucket(BucketName), 10)).WaitAsync(Timeout);
            Assert.All(harness.Nodes, n => Assert.Equal(BucketName, n.Owner));
            AssertClusterNodesKept(harness);

            await bucket.DisposeAsync().AsTask().WaitAsync(Timeout);

            AssertClusterNodesKept(harness);
        }

        // NCBC-4414
        // A new node is in the bucket map before it accepts the bucket. Each refused select closes the connection. The
        // pool tries a few replacements at once, then waits 30 s of real time for the scale controller. The node has no
        // connection meanwhile, so the operations for its vbuckets fail although it accepts the bucket after a few seconds.
        [Fact]
        public async Task NCBC4414_NewNodeThatRefusesTheBucketAtFirst_GetsConnectionsOnTheSdkClock()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap()).WaitAsync(Timeout);
            harness.Server.RefuseBucket(Node3, BucketName, ResponseStatus.KeyNotFound);
            var withNode3 = BucketMap(11).Node(Node3);
            harness.Server.SetMap(withNode3);

            harness.Publish(withNode3);
            await harness.DrainAsync().WaitAsync(Timeout);
            await harness.SettleAsync().WaitAsync(Timeout);
            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.DoesNotContain(harness.Server.ConnectionsOn(Node3), c => c.Bucket == BucketName);
            var refusedAtApply = harness.Server.Counters(Node3).SelectBucketsFailed;
            Assert.True(refusedAtApply > 0, $"{Node3} refused no select. {harness.Server.Counters(Node3)}");

            // The first wait between refill attempts.
            await harness.AdvanceAsync(TimeSpan.FromMilliseconds(32)).WaitAsync(Timeout);
            var refusedBeforeAccept = harness.Server.Counters(Node3).SelectBucketsFailed;
            harness.Server.AcceptBucket(Node3, BucketName);
            await harness.AdvanceAsync(TimeSpan.FromSeconds(10)).WaitAsync(Timeout);

            var node = (ClusterNode)bucket.Nodes.Single(n => harness.Server.NodeAt(n.EndPoint) == Node3);
            Assert.IsType<ChannelConnectionPool>(node.ConnectionPool);
            var connections = harness.Server.ConnectionsOn(Node3);
            Assert.True(connections.Count > 0 && connections.All(c => c.Bucket == BucketName),
                $"{Node3} accepts the bucket, but its connections are [{string.Join(", ", connections)}]. {harness.Server.Counters(Node3)}");
            await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
                bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node3)).WaitAsync(Timeout));
            Assert.Equal(1, harness.Server.ReceivedCount(Node3, OpCode.Get));
            Assert.True(refusedBeforeAccept > refusedAtApply,
                $"The pool did not retry the select on the clock. Refused {refusedAtApply} at apply and {refusedBeforeAccept} 32 ms later.");
        }

        private const string Node3 = "node-3.example.com";

        private static readonly string[] ThreeHosts = ["node-0.example.com", "node-1.example.com", "node-2.example.com"];

        private static ClusterMapBuilder ThreeKvNodes(ClusterMapBuilder map, ulong rev) => map.Rev(rev).Nodes(3, n => n.Kv().Query());

        private static ClusterMapBuilder WithQueryOnlyNode(ClusterMapBuilder map) => map.Node(Node3, n => n.Uuid("node-uuid-3").Query());

        private static void AssertClusterNodesKept(ConfigHarness harness)
        {
            var nodes = harness.Nodes;
            var described = string.Join(", ", nodes);
            Assert.True(ThreeHosts.All(h => nodes.Count(n => n.Name == h) == 1),
                $"Each of [{string.Join(", ", ThreeHosts)}] needs one node, but the context has [{described}].");
            var query = Record.Exception(() => harness.Context.GetRandomNodeForService(ServiceType.Query));
            Assert.True(query is null, $"Query picks no node, it throws {query?.GetType().Name}. The context has [{described}].");
        }

        private static ClusterMapBuilder WithService(ClusterMapBuilder map, string host, ServiceType service) =>
            service == ServiceType.Search ? map.Node(host, n => n.Search()) : map.Node(host, n => n.Analytics());

        // The endpoints of the context nodes that the bucket owns.
        private static List<HostEndpointWithPort> BucketEndPoints(ConfigHarness harness) => harness.Nodes
            .Where(n => n.Owner == BucketName)
            .Select(n => n.EndPoint)
            .OrderBy(e => e.ToString(), StringComparer.Ordinal)
            .ToList();

        private static ClusterProfile PrivateLink => ClusterProfile.Load(ClusterProfile.CapellaPrivateLink);

        private static void ConnectThroughPrivateEndpoint(ClusterOptions options)
        {
            options.ConnectionString = "couchbases://private-endpoint.example.com";
            options.NetworkResolution = NetworkResolution.Auto;
        }

        private static async Task PublishGlobalAsync(ConfigHarness harness, ClusterModel model)
        {
            var map = model.Render(model.Cluster, null);
            harness.Server.SetMap(map);
            harness.Publish(map);
            await harness.DrainAsync();
        }

        // Each global node as the node at its endpoint and the node at the endpoint of its adapter. Null is no node.
        private static List<(string? Node, string? Adapter)> GlobalNodes(ConfigHarness harness) => harness.Context.Nodes
            .Where(n => n.Owner is null)
            .Select(n => (harness.Server.NodeAt(n.EndPoint),
                n.NodesAdapter is { } adapter ? harness.Server.NodeAt(HostEndpointWithPort.Create(adapter, harness.Options)) : null))
            .OrderBy(n => n.Item1, StringComparer.Ordinal)
            .ToList();

        private static void AssertGlobalNodePerMapNode(ConfigHarness harness, ModelMap map)
        {
            var global = GlobalNodes(harness);

            // The seed node can stay or go.
            Assert.True(MapNodes(map).Select(n => (n, n)).SequenceEqual(global.Where(n => n.Node is not null)),
                $"Each of [{string.Join(", ", MapNodes(map))}] needs one global node at its own endpoint with its own " +
                $"adapter, but the global nodes are " +
                $"[{string.Join(", ", global.Select(n => $"{n.Node ?? "seed"} with the adapter of {n.Adapter ?? "none"}"))}].");
        }

        private static IEnumerable<string?> MapNodes(ModelMap map) =>
            map.Nodes.Select(n => (string?)n.Hostname).OrderBy(n => n, StringComparer.Ordinal);

        private static ClusterMapBuilder WithServerGroups(ClusterMapBuilder map) => map
            .Node("node-0.example.com", n => n.ServerGroup("group-1"))
            .Node("node-1.example.com", n => n.ServerGroup("group-2"));

        private static ClusterMapBuilder External(ClusterMapBuilder map) => map
            .Node("node-0.example.com", n => n.Uuid("node-uuid-0")
                .AlternateAddress("external", "203.0.113.10", p => p.Mgmt(32091, 32191).Kv(32210, 32207)))
            .Node("node-1.example.com", n => n.Uuid("node-uuid-1")
                .AlternateAddress("external", "203.0.113.11", p => p.Mgmt(32091, 32191).Kv(32210, 32207)));
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
