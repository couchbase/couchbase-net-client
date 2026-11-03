#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Configuration;
using Xunit;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigHarnessTests;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigPushTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// Each test asserts the correct behaviour for a known config handling bug, so it fails until the bug is fixed.
    /// </summary>
    public class ConfigHandlingBugTests
    {
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
            harness.Server.SetMap(GlobalMap(20).Node(Node0, n => n.Query(9093)), Node0);
            harness.Server.SetMap(GlobalMap(15), Node1);
            // Node-0 serves rev 20 once, so every later pass delivers only rev 15 from the lagging node-1.
            harness.Server.FailFetchesAfter(1, Node0);
            harness.StartPolling();

            // In either poll order, the second rev 15 comes after the only rev 20.
            await harness.AdvanceAsync(PollPasses(2));

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

            harness.Server.PushBrief("203.0.113.10", 1, 11);
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
