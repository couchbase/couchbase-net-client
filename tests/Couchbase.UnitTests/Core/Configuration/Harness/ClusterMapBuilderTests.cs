#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    public class ClusterMapBuilderTests
    {
        [Fact]
        public void Bucket_Build_ParsesNodesServicesAndVBucketMap()
        {
            var builder = ClusterMapBuilder.Bucket("travel-sample")
                .Rev(42).Epoch(3)
                .Node("node-a.example.com", n => n.Uuid("uuid-a").Kv().Query().ThisNode().ServerGroup("group-1"))
                .Node("node-b.example.com", n => n.Uuid("uuid-b").ServerGroup("group-2")
                    .AlternateAddress("external", "203.0.113.2", p => p.Mgmt(32790, 32773).Kv(32775, 32776)))
                .Node("node-c.example.com", n => n.Search().Analytics().Eventing())
                .VBuckets(64, replicas: 2)
                .BucketCapabilities("rangeScan")
                .ClusterCapabilities("n1ql", "enhancedPreparedStatements", "readFromReplica")
                .ClusterLabels("my-cluster", "cluster-uuid-1");

            var config = builder.Build();

            Assert.Equal("travel-sample", config.Name);
            Assert.False(config.IsGlobal);
            Assert.Equal("vbucket", config.NodeLocator);
            Assert.Equal(32, config.Uuid.Length);
            Assert.Equal(new ConfigVersion(3, 42), config.ConfigVersion);
            Assert.Equal("my-cluster", config.ClusterName);
            Assert.Equal("cluster-uuid-1", config.ClusterUuid);

            // The legacy nodes list holds only the data nodes.
            Assert.Equal(["node-a.example.com:8091", "node-b.example.com:8091"], config.Nodes.Select(n => n.Hostname));
            Assert.All(config.Nodes, n => Assert.Equal(11210, n.Ports.Direct));
            Assert.Equal(3, config.NodesExt.Count);
            Assert.Equal([true, false, false], config.NodesExt.Select(n => n.ThisNode));
            Assert.Equal(["uuid-a", "uuid-b", null], config.NodesExt.Select(n => n.NodeUuid));
            Assert.Equal(["group-1", "group-2", null], config.NodesExt.Select(n => n.ServerGroup));

            Assert.False(config.NodesExt[0].HasAlternateAddress);
            var alternate = config.NodesExt[1].AlternateAddresses["external"];
            Assert.Equal("203.0.113.2", alternate.Hostname);
            Assert.Equal((32790, 32773, 32775, 32776, 0), (alternate.Ports.Mgmt, alternate.Ports.MgmtSsl,
                alternate.Ports.Kv, alternate.Ports.KvSsl, alternate.Ports.N1Ql));

            config.NetworkResolution = NetworkResolution.Default;
            Assert.Equal(
            [
                "node-a.example.com mgmt=8091/18091 kv=11210/11207 views=8092/18092 query=8093/18093 search=0/0 analytics=0/0 eventing=0/0 uuid=uuid-a",
                "node-b.example.com mgmt=8091/18091 kv=11210/11207 views=8092/18092 query=0/0 search=0/0 analytics=0/0 eventing=0/0 uuid=uuid-b",
                "node-c.example.com mgmt=8091/18091 kv=0/0 views=0/0 query=0/0 search=8094/18094 analytics=8095/18095 eventing=8096/18096 uuid="
            ], config.GetNodes().Select(Describe));

            var map = config.VBucketServerMap;
            Assert.Equal(["node-a.example.com:11210", "node-b.example.com:11210"], map.ServerList);
            Assert.Equal(2, map.NumReplicas);
            Assert.Equal(64, map.VBucketMap.Length);
            Assert.All(map.VBucketMap, entry => Assert.Equal(3, entry.Length));
            Assert.Equal(new short[] { 0, 1, -1 }, map.VBucketMap[0]);
            Assert.Equal(new short[] { 1, 0, -1 }, map.VBucketMap[1]);
            Assert.Equal([0], config.ServerGroupNodeIndexes["group-1"]);
            Assert.Equal([1], config.ServerGroupNodeIndexes["group-2"]);

            Assert.True(config.HasBucketCap("cccp"));
            Assert.True(config.HasBucketCap("rangeScan"));
            Assert.True(config.HasClusterCap("n1ql.enhancedPreparedStatements"));
            Assert.True(config.HasClusterCap("n1ql.readFromReplica"));
            Assert.False(config.HasClusterCap("search.vectorSearch"));
        }

        [Fact]
        public void Global_Build_ParsesNodesAndAlternateAddresses()
        {
            var builder = ClusterMapBuilder.Global()
                .Rev(7).Epoch(2)
                .Nodes(2, n => n.Kv().Query())
                .Node("node-0.example.com", n => n.ThisNode().AlternateAddress("external", "198.51.100.10",
                    p => p.Mgmt(9091, 19091).Kv(9210, 9207).Query(9093, 19093)))
                .Node("node-1.example.com", n => n.AlternateAddress("external", "198.51.100.11"))
                .ClusterCapabilities("n1ql", "enhancedPreparedStatements")
                .ClusterLabels("my-cluster", "cluster-uuid-1");

            var config = builder.Build();

            Assert.Equal(BucketConfig.GlobalBucketName, config.Name);
            Assert.True(config.IsGlobal);
            Assert.Equal(new ConfigVersion(2, 7), config.ConfigVersion);
            Assert.Empty(config.Nodes);
            Assert.Empty(config.VBucketServerMap.ServerList);
            Assert.Empty(config.VBucketServerMap.VBucketMap);
            Assert.Empty(config.BucketCapabilities);
            Assert.Equal(["node-uuid-0", "node-uuid-1"], config.NodesExt.Select(n => n.NodeUuid));
            Assert.Equal([true, false], config.NodesExt.Select(n => n.ThisNode));
            Assert.True(config.HasClusterCap("n1ql.enhancedPreparedStatements"));
            Assert.Equal("cluster-uuid-1", config.ClusterUuid);

            config.NetworkResolution = NetworkResolution.Default;
            Assert.Equal(
            [
                "node-0.example.com mgmt=8091/18091 kv=11210/11207 views=8092/18092 query=8093/18093 search=0/0 analytics=0/0 eventing=0/0 uuid=node-uuid-0",
                "node-1.example.com mgmt=8091/18091 kv=11210/11207 views=8092/18092 query=8093/18093 search=0/0 analytics=0/0 eventing=0/0 uuid=node-uuid-1"
            ], config.GetNodes().Select(Describe));

            // An alternate address with ports offers only those ports. Without ports it keeps the node ports.
            var external = builder.Build();
            external.NetworkResolution = NetworkResolution.External;
            Assert.Equal(
            [
                "198.51.100.10 mgmt=9091/19091 kv=9210/9207 views=0/0 query=9093/19093 search=0/0 analytics=0/0 eventing=0/0 uuid=node-uuid-0",
                "198.51.100.11 mgmt=8091/18091 kv=11210/11207 views=8092/18092 query=8093/18093 search=0/0 analytics=0/0 eventing=0/0 uuid=node-uuid-1"
            ], external.GetNodes().Select(Describe));
        }

        [Fact]
        public void Global_BucketOnlySettings_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => ClusterMapBuilder.Global().VBuckets());
            Assert.Throws<InvalidOperationException>(() => ClusterMapBuilder.Global().BucketCapabilities("cccp"));
        }

        [Fact]
        public void Clone_WithNextRev_IsNewerAndLeavesOriginalUnchanged()
        {
            var original = ClusterMapBuilder.Bucket("default").Rev(10).Nodes(2);
            var originalJson = original.ToJson();

            var next = original.Clone().Rev(11).Node("node-0.example.com", n => n.Query()).Nodes(1);

            var oldConfig = original.Build();
            var newConfig = next.Build();
            Assert.True(newConfig.IsNewerThan(oldConfig));
            Assert.False(oldConfig.IsNewerThan(newConfig));
            Assert.True(newConfig.HasConfigChanges(oldConfig, "default"));
            Assert.False(oldConfig.HasConfigChanges(newConfig, "default"));

            Assert.Equal(originalJson, original.ToJson());
            Assert.Equal(new ConfigVersion(1, 10), oldConfig.ConfigVersion);
            Assert.Equal(2, oldConfig.NodesExt.Count);
            Assert.Equal(0, oldConfig.NodesExt[0].Services.N1Ql);
            Assert.Equal(3, newConfig.NodesExt.Count);
            Assert.Equal(8093, newConfig.NodesExt[0].Services.N1Ql);
        }

        [Fact]
        public void Clone_WithoutChanges_IsEqualAndNotNewer()
        {
            var original = ClusterMapBuilder.Bucket("default").Nodes(3);
            var clone = original.Clone();

            Assert.Equal(original.ToJson(), clone.ToJson());
            Assert.Equal(original.Build(), clone.Build());
            Assert.False(clone.Build().HasConfigChanges(original.Build(), "default"));
        }

        [Fact]
        public void Clone_WithHigherEpochAndLowerRev_IsNewer()
        {
            var original = ClusterMapBuilder.Bucket("default").Nodes(2).Epoch(1).Rev(100);

            var oldConfig = original.Build();
            var newConfig = original.Clone().Epoch(2).Rev(1).Build();

            Assert.True(newConfig.IsNewerThan(oldConfig));
            Assert.False(oldConfig.IsNewerThan(newConfig));
            Assert.True(newConfig.HasConfigChanges(oldConfig, "default"));
        }

        [Fact]
        public void HostPlaceholder_IsReplacedWithBootstrapHost()
        {
            var builder = ClusterMapBuilder.Bucket("default").Node(NodeBuilder.HostPlaceholder, n => n.ThisNode());
            Assert.Contains("\"$HOST:8091\"", builder.ToJson());

            var config = builder.Build();
            Assert.Null(config.NodesExt[0].Hostname);
            Assert.Equal("$HOST:8091", config.Nodes[0].Hostname);
            Assert.Equal("$HOST:11210", config.VBucketServerMap.ServerList[0]);

            config.ReplacePlaceholderWithBootstrapHost("10.0.0.5");

            Assert.Equal("10.0.0.5", config.NodesExt[0].Hostname);
            Assert.Equal("10.0.0.5:8091", config.Nodes[0].Hostname);
            Assert.Equal(["10.0.0.5:11210"], config.VBucketServerMap.ServerList);
            Assert.Equal("10.0.0.5", Assert.Single(config.GetNodes()).Hostname);
        }

        [Fact]
        public void RemoveNode_RemovesItEverywhereAndRebuildsVBucketMap()
        {
            var builder = ClusterMapBuilder.Bucket("default").Nodes(3).VBuckets(1024, 1);
            var before = builder.Build();

            var after = builder.Clone().Rev(2).RemoveNode("node-1.example.com").Build();

            Assert.Equal(3, before.VBucketServerMap.ServerList.Length);
            Assert.Equal(["node-0.example.com", "node-2.example.com"], after.NodesExt.Select(n => n.Hostname));
            Assert.Equal(["node-0.example.com:8091", "node-2.example.com:8091"], after.Nodes.Select(n => n.Hostname));
            Assert.Equal(["node-0.example.com:11210", "node-2.example.com:11210"], after.VBucketServerMap.ServerList);
            Assert.Equal(1024, after.VBucketServerMap.VBucketMap.Length);
            Assert.All(after.VBucketServerMap.VBucketMap, entry => Assert.Equal(1, entry[0] + entry[1]));
            Assert.Equal([512, 512], ActiveCopiesPerServer(after));
            Assert.True(after.HasClusterNodesChanged(before));
            Assert.True(after.HasVBucketMapChanged(before, out _));
            Assert.Throws<ArgumentException>(() => builder.RemoveNode("missing.example.com"));
        }

        [Fact]
        public void Build_MatchesRealServerFixture()
        {
            var fixture = ResourceHelper.ReadResource(@"Documents\Configs\rev94.json",
                InternalSerializationContext.Default.BucketConfig);

            var built = ClusterMapBuilder.Bucket("default").Rev(94).Epoch(0)
                .Node("10.112.192.101", n => n.Kv().Query().Search().Analytics().ThisNode())
                .Node("10.112.192.102", n => n.Kv().Query())
                .VBuckets(1024, 1)
                .Build();

            Assert.Equal(fixture.ConfigVersion, built.ConfigVersion);
            Assert.Equal(fixture.Name, built.Name);
            Assert.Equal(fixture.NodeLocator, built.NodeLocator);
            Assert.Equal(fixture.Nodes.Select(n => (n.Hostname, n.Ports.Direct)), built.Nodes.Select(n => (n.Hostname, n.Ports.Direct)));
            Assert.Equal(fixture.NodesExt.Select(n => (n.Hostname, n.ThisNode)), built.NodesExt.Select(n => (n.Hostname, n.ThisNode)));
            Assert.Equal(fixture.GetNodes().Select(Describe), built.GetNodes().Select(Describe));

            var expected = fixture.VBucketServerMap;
            var actual = built.VBucketServerMap;
            Assert.Equal(expected.HashAlgorithm, actual.HashAlgorithm);
            Assert.Equal(expected.NumReplicas, actual.NumReplicas);
            Assert.Equal(expected.ServerList, actual.ServerList);
            Assert.Equal(expected.VBucketMap.Length, actual.VBucketMap.Length);
            Assert.Equal(expected.VBucketMap.Select(e => e.Length).Distinct(), actual.VBucketMap.Select(e => e.Length).Distinct());
            Assert.Equal(ActiveCopiesPerServer(fixture), ActiveCopiesPerServer(built));
            Assert.True(fixture.HasBucketCap("cccp"));
            Assert.True(built.HasBucketCap("cccp"));
        }

        [Fact]
        public void Build_HundredNodesAndFullVBucketMap()
        {
            var config = ClusterMapBuilder.Bucket("large").Nodes(100).VBuckets(1024, 3).Build();

            Assert.Equal(100, config.NodesExt.Count);
            Assert.Equal(100, config.Nodes.Count);
            Assert.Equal(100, config.VBucketServerMap.ServerList.Length);
            Assert.Equal(100, config.GetNodes().Count);
            Assert.Equal(1024, config.VBucketServerMap.VBucketMap.Length);
            Assert.All(config.VBucketServerMap.VBucketMap, entry =>
            {
                Assert.Equal(4, entry.Distinct().Count());
                Assert.All(entry, index => Assert.InRange(index, (short)0, (short)99));
            });
        }

        private static string Describe(NodeAdapter node) =>
            $"{node.Hostname} mgmt={node.MgmtApi}/{node.MgmtApiSsl} kv={node.KeyValue}/{node.KeyValueSsl} " +
            $"views={node.Views}/{node.ViewsSsl} query={node.N1Ql}/{node.N1QlSsl} search={node.Fts}/{node.FtsSsl} " +
            $"analytics={node.Analytics}/{node.AnalyticsSsl} eventing={node.Eventing}/{node.EventingSsl} uuid={node.NodeUuid}";

        private static IEnumerable<int> ActiveCopiesPerServer(BucketConfig config) =>
            config.VBucketServerMap.VBucketMap.GroupBy(entry => entry[0]).OrderBy(g => g.Key).Select(g => g.Count());
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
