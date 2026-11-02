#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Utils;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    public class ConfigHarnessTests
    {
        internal const string BucketName = "travel";
        internal const string QueryNode = "node-2.example.com";

        /// <summary>
        /// Two data and query nodes, plus one query only node.
        /// </summary>
        internal static ClusterMapBuilder GlobalMap(ulong rev = 10) => WithNodes(ClusterMapBuilder.Global(), rev);

        internal static ClusterMapBuilder BucketMap(ulong rev = 10) => WithNodes(ClusterMapBuilder.Bucket(BucketName), rev);

        internal static ConfigVersion Rev(ulong rev) => new(1, rev);

        private static ClusterMapBuilder WithNodes(ClusterMapBuilder map, ulong rev) => map.Rev(rev)
            .Nodes(2, n => n.Kv().Query())
            .Node(QueryNode, n => n.Uuid("node-uuid-2").Query());

        internal static ClusterMapBuilder WithAlternateAddresses(ClusterMapBuilder map, params int[] nodes)
        {
            foreach (var i in nodes)
            {
                map.Node($"node-{i}.example.com", n => n.Uuid($"node-uuid-{i}")
                    .AlternateAddress("external", $"203.0.113.{10 + i}", p => p.Mgmt(32091, 32191).Kv(32210, 32207)));
            }

            return map;
        }

        [Fact]
        public async Task Create_BootstrapsGlobalNodes()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            Assert.Equal(Rev(10), harness.GlobalVersion);
            Assert.Equal(3, harness.NodesCreated);
            Assert.Equal(["node-0.example.com", "node-1.example.com", QueryNode], harness.Nodes.Select(n => n.Hostname));
            Assert.Equal(["node-uuid-0", "node-uuid-1", "node-uuid-2"], harness.Nodes.Select(n => n.NodeUuid));
            Assert.All(harness.Nodes, n =>
            {
                Assert.Null(n.Owner);
                Assert.Equal(Rev(10), n.Version);
            });
            Assert.Equal(new Uri("http://node-0.example.com:8093/query/service"), harness.Nodes[0].QueryUri);
        }

        [Fact]
        public async Task OpenBucketAsync_AssignsDataNodesToBucket()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            var bucket = await harness.OpenBucketAsync(BucketMap());

            var view = harness.Bucket(BucketName);
            Assert.Equal(Rev(10), view.Version);
            Assert.Equal(10UL, view.KeyMapperRev);
            Assert.Equal(["node-0.example.com", "node-1.example.com"], view.Nodes);
            Assert.Equal([BucketName, BucketName, null], harness.Nodes.Select(n => n.Owner));
            Assert.Equal(3, harness.NodesCreated);
            Assert.True(bucket.SupportsCollections);
        }

        [Fact]
        public async Task NewerGlobalMap_WithNewHost_AddsNode()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            await harness.ApplyGlobalAsync(GlobalMap(11).Nodes(1).Build());

            Assert.Equal(4, harness.NodesCreated);
            Assert.Equal("node-3.example.com", harness.Nodes.Last().Hostname);
            Assert.All(harness.Nodes, n => Assert.Equal(Rev(11), n.Version));
            Assert.Equal(Rev(11), harness.GlobalVersion);
        }

        [Fact]
        public async Task NewerGlobalMap_WithoutHost_PrunesNode()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            await harness.ApplyGlobalAsync(GlobalMap(11).RemoveNode(QueryNode).Build());

            Assert.Equal(["node-0.example.com", "node-1.example.com"], harness.Nodes.Select(n => n.Hostname));
            Assert.All(harness.Nodes, n => Assert.Equal(Rev(11), n.Version));
        }

        [Fact]
        public async Task DuplicateMaps_ChangeNothing()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var adapters = harness.Context.Nodes.Select(n => n.NodesAdapter).ToList();
            var config = bucket.CurrentConfig;
            var keyMapper = bucket.KeyMapper;

            harness.Publish(GlobalMap());
            harness.Publish(BucketMap());
            await harness.DrainAsync();

            Assert.Equal(adapters, harness.Context.Nodes.Select(n => n.NodesAdapter));
            Assert.Same(config, bucket.CurrentConfig);
            Assert.Same(keyMapper, bucket.KeyMapper);
            Assert.Equal(3, harness.NodesCreated);
        }

        [Fact]
        public async Task BucketMap_WithVBucketChange_RebuildsKeyMapper()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var keyMapper = bucket.KeyMapper;

            harness.Publish(BucketMap(11).VBuckets(1024, replicas: 2));
            await harness.DrainAsync();

            var view = harness.Bucket(BucketName);
            Assert.NotSame(keyMapper, bucket.KeyMapper);
            Assert.Equal(11UL, view.KeyMapperRev);
            Assert.Equal(Rev(11), view.Version);
            Assert.Equal(2, bucket.CurrentConfig!.VBucketServerMap.NumReplicas);
            Assert.Equal(["node-0.example.com", "node-1.example.com"], view.Nodes);
            Assert.Equal(3, harness.NodesCreated);
        }

        [Fact]
        public async Task PublishThenDrain_ProcessesConfigsInOrder()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());

            // Out of order, rev 13 would run before node-3 exists and leave node-3 in place.
            harness.Publish(GlobalMap(11).Node("node-3.example.com"));
            harness.Publish(GlobalMap(12).Node("node-3.example.com").Node("node-4.example.com"));
            harness.Publish(GlobalMap(13).Node("node-4.example.com"));
            await harness.DrainAsync();

            Assert.Equal(["node-0.example.com", "node-1.example.com", QueryNode, "node-4.example.com"],
                harness.Nodes.Select(n => n.Hostname));
            Assert.Equal(5, harness.NodesCreated);
            Assert.All(harness.Nodes, n => Assert.Equal(Rev(13), n.Version));
        }

        [Fact]
        public async Task OlderBucketMap_IsIgnoredByBucket()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());

            harness.Publish(BucketMap(12).VBuckets(1024, replicas: 2));
            harness.Publish(BucketMap(11).VBuckets(1024, replicas: 0));
            await harness.DrainAsync();

            var view = harness.Bucket(BucketName);
            Assert.Equal(Rev(12), view.Version);
            Assert.Equal(12UL, view.KeyMapperRev);
            Assert.Equal(2, bucket.CurrentConfig!.VBucketServerMap.NumReplicas);
        }

        [Fact]
        public async Task AdvanceAsync_StepsFromTimerToTimer()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), pollInterval: TimeSpan.FromSeconds(1));
            var clock = (SteppableClock)harness.Clock;
            var start = clock.GetUtcNow();
            harness.StartPolling();

            // One jump to 3.5 s would run one pass, and the next pass would be due at 4.5 s.
            await harness.AdvanceAsync(TimeSpan.FromSeconds(3.5));

            // Each pass fetches node-0 and node-1.
            Assert.Equal(6, harness.Server.Fetches.Count);
            Assert.Equal(start.AddSeconds(3.5), clock.GetUtcNow());
            Assert.Equal(start.AddSeconds(4), clock.NextDueTime);
        }

        // The network chosen at bootstrap must not depend on the order of nodesExt.
        [Theory]
        [InlineData(NetworkResolution.External)]
        [InlineData(NetworkResolution.Auto)]
        public async Task NewerGlobalMap_WithSameNodesReordered_KeepsExternalNetwork(string network)
        {
            var map = WithAlternateAddresses(ClusterMapBuilder.Global().Rev(10), 1, 2)
                .Node("node-0.example.com", n => n.Uuid("node-uuid-0"));
            await using var harness = ConfigHarness.Create(map, options => options.NetworkResolution = network);
            Assert.Equal(NetworkResolution.External, harness.Options.EffectiveNetworkResolution);

            var reordered = ClusterMapBuilder.Global().Rev(11).Node("node-0.example.com", n => n.Uuid("node-uuid-0"));
            harness.Publish(WithAlternateAddresses(reordered, 1, 2));
            await harness.DrainAsync();

            Assert.Equal(NetworkResolution.External, harness.Options.EffectiveNetworkResolution);
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
