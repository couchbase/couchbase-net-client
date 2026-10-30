#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;
using Xunit;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigHarnessTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// Config paths that go over KV, which are CCCP fetches, clustermap change notifications and polling.
    /// </summary>
    public class ConfigPushTests
    {
        internal const string Node0 = "node-0.example.com";
        internal const string Node1 = "node-1.example.com";
        internal const string Global = BucketConfig.GlobalBucketName;
        internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

        [Fact]
        public async Task BriefPush_ForNewerVersion_FetchesWithKnownVersionAndAppliesMap()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.Server.ClearFetches();
            harness.Server.SetMap(BucketMap(11).VBuckets(1024, replicas: 2));

            harness.Server.PushBrief(Node0, 1, 11);
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            await harness.DrainAsync();

            var fetch = Assert.Single(harness.Server.Fetches);
            Assert.Equal(BucketName, fetch.Bucket);
            Assert.Equal(Rev(10), fetch.KnownVersion);
            Assert.Equal(Rev(11), fetch.Served);
            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.Equal(2, bucket.CurrentConfig!.VBucketServerMap.NumReplicas);
        }

        [Fact]
        public async Task BriefPush_ForCurrentOrOlderVersion_DoesNotFetch()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            harness.Server.ClearFetches();

            harness.Server.PushBrief(Node0, 1, 10);
            harness.Server.PushBrief(Node1, 1, 9);
            harness.Server.PushBrief(Node0, 0, 50);

            // The bucket drops these pushes before it wakes its fetch loop.
            Assert.Empty(harness.Server.Fetches);

            harness.Server.SetMap(BucketMap(11).VBuckets(1024, replicas: 2));
            harness.Server.PushBrief(Node0, 1, 11);
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            await harness.DrainAsync();

            Assert.Equal(Rev(10), Assert.Single(harness.Server.Fetches).KnownVersion);
            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
        }

        [Fact]
        public async Task FullPush_AppliesMap()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.Server.ClearFetches();

            harness.Server.PushFull(Node1, BucketMap(11).VBuckets(1024, replicas: 2));
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            await harness.DrainAsync();

            Assert.Empty(harness.Server.Fetches);
            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.Equal(2, bucket.CurrentConfig!.VBucketServerMap.NumReplicas);
            Assert.Equal([Node0, Node1], harness.Bucket(BucketName).Nodes);
        }

        [Fact]
        public async Task BriefPushStorm_ForSameVersion_FetchesOnce()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            harness.Server.ClearFetches();
            harness.Server.SetMap(BucketMap(11).VBuckets(1024, replicas: 2));

            for (var i = 0; i < 100; i++)
            {
                harness.Server.PushBrief(i % 2 == 0 ? Node0 : Node1, 1, 11);
            }
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            await harness.DrainAsync();

            Assert.Single(harness.Server.Fetches);
            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
        }

        [Fact]
        public async Task BriefPush_WhileNodesLag_AppliesPushedVersionOnceANodeServesIt()
        {
            await using var harness = ConfigHarness.Create(GlobalMap());
            await harness.OpenBucketAsync(BucketMap());
            harness.Server.ClearFetches();

            // Node-1 stays on rev 10. Node-0 has nothing newer for its next three fetches, so three fetch passes find nothing.
            harness.Server.SetMapAfter(3, BucketMap(11).VBuckets(1024, replicas: 2), Node0);
            harness.Server.PushBrief(Node0, 1, 11);
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            await harness.DrainAsync();

            Assert.Equal(Rev(11), harness.Bucket(BucketName).Version);
            Assert.Equal(4, harness.Server.FetchCount(Node0));
            Assert.InRange(harness.Server.FetchCount(Node1), 3, 4);
            Assert.All(harness.Server.Fetches, f => Assert.Equal(Rev(10), f.KnownVersion));
        }

        [Fact]
        public async Task Polling_WithAllNodesOnSameMap_AppliesItOnce()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), pollInterval: PollInterval);
            harness.Server.SetMap(GlobalMap(11).Node("node-3.example.com"));
            harness.StartPolling();

            // The first pass fetches node-0 and node-1. Each later pass also fetches node-3.
            await harness.WaitForPublishedAsync(Global, Rev(11), Timeout, times: 2);
            await harness.DrainAsync();
            var adapters = harness.Context.Nodes.ToDictionary(n => n.EndPoint.Host, n => n.NodesAdapter);

            await harness.WaitForPublishedAsync(Global, Rev(11), Timeout, times: 8);
            await harness.DrainAsync();

            Assert.Equal(Rev(11), harness.GlobalVersion);
            Assert.Equal(4, harness.NodesCreated);
            Assert.All(harness.Context.Nodes, n => Assert.Same(adapters[n.EndPoint.Host], n.NodesAdapter));
            Assert.All(harness.Server.Fetches, f => Assert.Null(f.KnownVersion));
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
