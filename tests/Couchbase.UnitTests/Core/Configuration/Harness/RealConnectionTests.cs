#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Test.Common.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigHarnessTests;
using static Couchbase.UnitTests.Core.Configuration.Harness.ConfigPushTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    /// <summary>
    /// The harness in real mode. The nodes reach the fake server through the SDK pools and connections, over streams that
    /// split the frames.
    /// </summary>
    public class RealConnectionTests
    {
        internal static readonly TimeSpan SmallKvTimeout = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// How long a test waits to show that something does not happen.
        /// </summary>
        private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(200);

        [Fact]
        public async Task KvOpAndConfigFetch_OverFragmentedStreams_Complete()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var node = harness.Context.Nodes.Single(n => n.Owner == bucket && n.EndPoint.Host == Node0);

            await Assert.ThrowsAsync<DocumentNotFoundException>(() => bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node0)));
            var config = await node.GetClusterMap();

            Assert.Equal(Rev(10), config.ConfigVersion);
            var pool = Assert.IsType<ChannelConnectionPool>(((ClusterNode)node).ConnectionPool);
            Assert.All(pool.GetConnections(), c => Assert.IsType<MultiplexingConnection>(c));
            Assert.Equal(1, harness.Server.ReceivedCount(Node0, OpCode.Get));
            var counters = harness.Server.Counters(Node0);
            Assert.Equal(2, counters.OpenConnections);
            Assert.Equal(counters.RequestsReceived, counters.RequestsAnswered);
            Assert.True(counters.PartialReadsByServer > 0, $"The node read no partial frame. {counters}");
            Assert.True(counters.PartialReadsBySdk > 0, $"The SDK read no partial frame. {counters}");
            Assert.All(harness.Server.ConnectionsOn(Node0), c => Assert.Equal(BucketName, c.Bucket));
        }

        [Fact]
        public async Task BriefAndFullPushes_ReachSdkThroughReceiveLoop()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.Server.ClearFetches();
            harness.Server.SetMap(BucketMap(11).VBuckets(1024, replicas: 2));

            harness.Server.PushBrief(Node0, 1, 11);
            await harness.WaitForPublishedAsync(BucketName, Rev(11), Timeout);
            harness.Server.PushFull(Node1, BucketMap(12).VBuckets(1024, replicas: 1));
            await harness.WaitForPublishedAsync(BucketName, Rev(12), Timeout);
            await harness.DrainAsync();

            // Only the brief push needs a fetch.
            var fetch = Assert.Single(harness.Server.Fetches);
            Assert.Equal(Rev(10), fetch.KnownVersion);
            Assert.Equal(Rev(11), fetch.Served);
            Assert.Equal(Rev(12), harness.Bucket(BucketName).Version);
            Assert.Equal(1, bucket.CurrentConfig!.VBucketServerMap.NumReplicas);
        }

        [Fact]
        public async Task Hang_TimesOutKvOp_AndResumeDeliversHeldReplyAsOrphan()
        {
            // One connection with one in-flight slot, so the next op can only go out once the late reply frees the slot.
            await using var harness = ConfigHarness.Create(GlobalMap(), options =>
            {
                options.KvTimeout = SmallKvTimeout;
                OneConnection(options, inFlight: 1);
            }, clock: TimeProvider.System, mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var node = harness.Context.Nodes.Single(n => n.Owner == bucket && n.EndPoint.Host == Node0);
            Assert.IsType<SingleConnectionPool>(((ClusterNode)node).ConnectionPool);
            var key = KeyOn(bucket, Node0);
            var before = harness.Server.Counters(Node0);
            harness.Server.Hang(Node0);

            var elapsed = Stopwatch.StartNew();
            await Assert.ThrowsAsync<UnambiguousTimeoutException>(() => bucket.DefaultCollection().GetAsync(key));
            elapsed.Stop();
            Assert.Equal(1, harness.Server.Counters(Node0).RepliesHeld);

            harness.Server.Resume(Node0);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).RequestsAnswered == before.RequestsAnswered + 1,
                Timeout, "The held reply going out");
            await Assert.ThrowsAsync<DocumentNotFoundException>(() => bucket.DefaultCollection().GetAsync(key));

            Assert.InRange(elapsed.Elapsed, SmallKvTimeout - TimeSpan.FromMilliseconds(20), Timeout);
            var after = harness.Server.Counters(Node0);
            Assert.Equal(before.ConnectionsOpened, after.ConnectionsOpened);
            Assert.Equal(before.ConnectionsClosed, after.ConnectionsClosed);
            Assert.Equal(2, harness.Server.ReceivedCount(Node0, OpCode.Get));
        }

        [Fact]
        public async Task Stall_BlocksWritesOnceBufferIsFull_AndResumeUnblocksThem()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), options => OneConnection(options, inFlight: 1024),
                mode: ConnectionMode.Real, network: new NetworkOptions { BufferSize = 1024 });
            var bucket = await harness.OpenBucketAsync(BucketMap());
            // Each request is about 150 bytes, so a few fill the buffer.
            var keys = KeysOn(bucket, Node0, padding: 120).Take(40).ToList();
            harness.Server.Stall(Node0);

            var gets = keys.Select(k => bucket.DefaultCollection().GetAsync(k)).ToList();
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).BlockedWrites == 1, Timeout, "An SDK write blocking");
            await Task.Delay(Quiet);

            Assert.Equal(0, harness.Server.ReceivedCount(Node0, OpCode.Get));
            Assert.Equal(1, harness.Server.Counters(Node0).BlockedWrites);
            Assert.DoesNotContain(gets, g => g.IsCompleted);

            harness.Server.Resume(Node0);
            foreach (var get in gets)
            {
                await Assert.ThrowsAsync<DocumentNotFoundException>(() => get);
            }

            Assert.Equal(keys.Count, harness.Server.ReceivedCount(Node0, OpCode.Get));
            Assert.Equal(0, harness.Server.Counters(Node0).BlockedWrites);
        }

        /// <summary>
        /// The request is much bigger than the buffer, so the KV timeout cancels its write after part of it went out.
        /// The SDK closes the connection, so the node never reads past the cut frame and never applies the upsert.
        /// The pool opens a new connection.
        /// </summary>
        [Fact]
        public async Task Stall_WithWriteCancelledInsideFrame_ClosesConnection()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), options =>
            {
                options.KvTimeout = SmallKvTimeout;
                OneConnection(options, inFlight: 16);
            }, clock: TimeProvider.System, mode: ConnectionMode.Real, network: new NetworkOptions { BufferSize = 1024 });
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var key = KeyOn(bucket, Node0);
            var before = harness.Server.Counters(Node0);
            harness.Server.Stall(Node0);

            await Assert.ThrowsAsync<UnambiguousTimeoutException>(() =>
                bucket.DefaultCollection().UpsertAsync(key, new string('x', 64 * 1024)));

            harness.Server.Resume(Node0);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).ConnectionsClosed == before.ConnectionsClosed + 1,
                Timeout, "The node seeing the connection close");
            await Assert.ThrowsAsync<DocumentNotFoundException>(() => bucket.DefaultCollection().GetAsync(key));

            var after = harness.Server.Counters(Node0);
            Assert.Equal(before.ConnectionsOpened + 1, after.ConnectionsOpened);
            Assert.Equal(0, harness.Server.ReceivedCount(Node0, OpCode.Set));
        }

        [Fact]
        public async Task Close_EndsConnections_AndPoolOpensNewOnes()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var before = harness.Server.Counters(Node0);
            var closed = harness.Server.ConnectionsOn(Node0).Select(c => c.Id).ToList();

            harness.Server.Close(Node0);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).OpenConnections == 2, Timeout,
                "The pool replacing both connections");
            await Assert.ThrowsAsync<DocumentNotFoundException>(() => bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node0)));

            var after = harness.Server.Counters(Node0);
            Assert.Equal(2, closed.Count);
            Assert.Equal(before.ConnectionsClosed + 2, after.ConnectionsClosed);
            Assert.Equal(before.ConnectionsOpened + 2, after.ConnectionsOpened);
            Assert.All(harness.Server.ConnectionsOn(Node0), c =>
            {
                Assert.DoesNotContain(c.Id, closed);
                Assert.Equal(BucketName, c.Bucket);
            });
        }

        [Fact]
        public async Task Close_WhileRefusing_NewConnectionsFailUntilRefuseIsOff()
        {
            // The scale controller adds the connections the pool could not replace, on each pass.
            await using var harness = ConfigHarness.Create(GlobalMap(), options =>
                    options.AddClusterService<IConnectionPoolScaleControllerFactory, FastScaleControllerFactory>(sp =>
                        new FastScaleControllerFactory(sp.GetRequiredService<Redactor>(),
                            sp.GetRequiredService<ILogger<DefaultConnectionPoolScaleController>>())),
                mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var opened = harness.Server.Counters(Node0).ConnectionsOpened;

            harness.Server.Refuse(Node0, true);
            harness.Server.Close(Node0);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).ConnectionsRefused >= 6, Timeout,
                "Six refused connects");
            var get = bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node0));
            await Task.Delay(Quiet);

            Assert.Equal(0, harness.Server.Counters(Node0).OpenConnections);
            Assert.Equal(opened, harness.Server.Counters(Node0).ConnectionsOpened);
            Assert.False(get.IsCompleted);

            harness.Server.Refuse(Node0, false);
            await Assert.ThrowsAsync<DocumentNotFoundException>(() => get);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).OpenConnections == 2, Timeout,
                "The scale controller opening both connections");
        }

        [Fact]
        public async Task InFlightCapOfOne_WithHungHost_SecondOpWaitsForSlot()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), options => OneConnection(options, inFlight: 1),
                mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            var keys = KeysOn(bucket, Node0).Take(2).ToList();
            harness.Server.Hang(Node0);

            var first = bucket.DefaultCollection().GetAsync(keys[0]);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).RepliesHeld == 1, Timeout, "The first Get reaching node-0");
            var second = bucket.DefaultCollection().GetAsync(keys[1]);
            await Task.Delay(Quiet);

            Assert.Equal(1, harness.Server.ReceivedCount(Node0, OpCode.Get));
            Assert.False(second.IsCompleted);

            harness.Server.Resume(Node0);
            await Assert.ThrowsAsync<DocumentNotFoundException>(() => first);
            await Assert.ThrowsAsync<DocumentNotFoundException>(() => second);
            Assert.Equal(2, harness.Server.ReceivedCount(Node0, OpCode.Get));
        }

        /// <summary>
        /// With the default pool of 2 connections, each connection holds one op and the others wait in the pool.
        /// </summary>
        [Fact]
        public async Task InFlightCapOfOne_WithHungHost_HoldsOneOpPerConnection()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(),
                options => options.Tuning.MaximumInFlightOperationsPerConnection = 1, mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.Server.Hang(Node0);

            var gets = KeysOn(bucket, Node0).Take(4).Select(k => bucket.DefaultCollection().GetAsync(k)).ToList();
            await harness.WaitUntilAsync(() => harness.Server.ConnectionsOn(Node0) is { Count: 2 } open && open.All(c => c.RepliesHeld == 1), Timeout,
                "Each node-0 connection holding one Get");
            await Task.Delay(Quiet);

            Assert.Equal(2, harness.Server.ReceivedCount(Node0, OpCode.Get));
            Assert.Equal([1, 1], harness.Server.ConnectionsOn(Node0).Select(c => c.RepliesHeld));

            harness.Server.Resume(Node0);
            foreach (var get in gets)
            {
                await Assert.ThrowsAsync<DocumentNotFoundException>(() => get);
            }
            Assert.Equal(4, harness.Server.ReceivedCount(Node0, OpCode.Get));
        }

        [Fact]
        public async Task SettleAsync_WithHeldReplyAndStalledHost_GoesQuiet()
        {
            await using var harness = ConfigHarness.Create(GlobalMap(), mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.Server.Hang(Node0);
            harness.Server.Stall(Node1);

            var held = bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node0));
            var unread = bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node1));
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).RepliesHeld == 1, Timeout, "The Get reaching node-0");
            await harness.SettleAsync();

            Assert.False(held.IsCompleted);
            Assert.False(unread.IsCompleted);
            Assert.Equal(0, harness.Server.ReceivedCount(Node1, OpCode.Get));
        }

        [Fact]
        public async Task DisposeAsync_WithHungAndStalledHosts_FinishesFast()
        {
            var harness = ConfigHarness.Create(GlobalMap(), mode: ConnectionMode.Real);
            var bucket = await harness.OpenBucketAsync(BucketMap());
            harness.Server.Hang(Node0);
            harness.Server.Stall(Node1);
            _ = bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node0));
            _ = bucket.DefaultCollection().GetAsync(KeyOn(bucket, Node1));
            // The bucket refresher fetches from node-0 too, or waits on node-1 first.
            harness.Server.PushBrief(Node1, 1, 11);
            await harness.WaitUntilAsync(() => harness.Server.Counters(Node0).RepliesHeld >= 1, Timeout, "A request reaching node-0");

            var elapsed = Stopwatch.StartNew();
            await harness.DisposeAsync();

            Assert.InRange(elapsed.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            Assert.Empty(harness.Server.ConnectionsOn(Node0));
            Assert.Empty(harness.Server.ConnectionsOn(Node1));
        }

        /// <summary>
        /// A new node on a hung host gets the setup requests of both connections and answers none. Each request times
        /// out after KvTimeout of real time, even on the harness clock, and the SDK closes both connections.
        /// </summary>
        [Fact]
        public async Task NewGlobalNode_OnHungHost_GetsSetupRequestsAndNoReply()
        {
            const string node3 = "node-3.example.com";
            await using var harness = ConfigHarness.Create(GlobalMap(), options => options.KvTimeout = SmallKvTimeout,
                mode: ConnectionMode.Real);
            harness.Server.Hang(node3);

            harness.Publish(GlobalMap(11).Nodes(1));
            await harness.DrainAsync();

            Assert.Equal(2, harness.Server.ReceivedCount(node3, OpCode.Helo));
            var counters = harness.Server.Counters(node3);
            Assert.Equal(2, counters.ConnectionsClosed);
            Assert.Equal(0, counters.RequestsAnswered);
        }

        private static void OneConnection(ClusterOptions options, int inFlight)
        {
            options.NumKvConnections = 1;
            options.MaxKvConnections = 1;
            options.Tuning.MaximumInFlightOperationsPerConnection = inFlight;
        }

        /// <summary>
        /// Keys whose active copy is on the host. The padding makes each request longer.
        /// </summary>
        internal static IEnumerable<string> KeysOn(CouchbaseBucket bucket, string host, int padding = 0) =>
            Enumerable.Range(0, 10_000)
                .Select(i => $"key-{i}{new string('x', padding)}")
                .Where(k => bucket.KeyMapper!.MapKey(k).LocatePrimary()?.Host == host);

        internal static string KeyOn(CouchbaseBucket bucket, string host) => KeysOn(bucket, host).First();

        /// <summary>
        /// The SDK scale controller with a short pass interval, so a test sees it add connections.
        /// </summary>
        private sealed class FastScaleControllerFactory(Redactor redactor, ILogger<DefaultConnectionPoolScaleController> logger)
            : IConnectionPoolScaleControllerFactory
        {
            public IConnectionPoolScaleController Create() =>
                new DefaultConnectionPoolScaleController(redactor, logger) { PollInterval = TimeSpan.FromMilliseconds(50) };
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
