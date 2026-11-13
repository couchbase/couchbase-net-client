using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.RangeScan;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    /// <summary>
    /// ScanAsync cancellation and cleanup (NCBC-4365). The bucket has two partitions. Each
    /// RangeScanContinue stays pending until the test finishes it or its token is cancelled, so the
    /// result does not depend on the scheduler. The scan reads the partitions in a random order, so a
    /// test asks the bucket which partition it is reading. There is no wall-clock bound: a read that is
    /// never cancelled makes the test wait, and CI's --blame-hang fails a hung run and keeps a dump.
    /// </summary>
    public class CouchbaseCollectionScanTests
    {
        // The hex form of the UUID that the fake bucket returns for every range scan create.
        private const string FakeRangeScanId = "0xa0a1a2a3a4a5a6a7a8a9aaabacadaeaf";

        [Fact]
        public async Task WithCancellation_Cancels_The_Read_And_Closes_The_Open_Scans()
        {
            var (collection, bucket) = CreateCollection();
            using var cts = new CancellationTokenSource();

            await using var stream = collection.ScanAsync(new RangeScan())
                .WithCancellation(cts.Token).GetAsyncEnumerator();

            var moved = stream.MoveNextAsync();
            var first = await bucket.NextContinue();
            bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a");
            Assert.True(await moved);

            moved = stream.MoveNextAsync();
            var second = await bucket.NextContinue();
            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            await bucket.WhenClosed(first);
            await bucket.WhenClosed(second);
        }

        // Before NCBC-4365 the token was examined once for each round of all partitions, so a cancel
        // between reads ended the stream with no error.
        [Fact]
        public async Task WithCancellation_Between_Reads_Throws()
        {
            var (collection, bucket) = CreateCollection();
            using var cts = new CancellationTokenSource();

            await using var stream = collection.ScanAsync(new RangeScan())
                .WithCancellation(cts.Token).GetAsyncEnumerator();

            var moved = stream.MoveNextAsync();
            var first = await bucket.NextContinue();
            bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a");
            Assert.True(await moved);

            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());

            Assert.Equal(cts.Token, canceled.CancellationToken);
            await bucket.WhenClosed(first);
        }

        [Fact]
        public async Task Options_Token_Still_Cancels_The_Scan()
        {
            var (collection, bucket) = CreateCollection();
            using var cts = new CancellationTokenSource();

            await using var stream = collection.ScanAsync(new RangeScan(), new ScanOptions().Token(cts.Token))
                .GetAsyncEnumerator();

            // Awaited as a ValueTask, as in the other tests: on .NET Framework, ValueTask.AsTask() completes the
            // task as cancelled without the exception's token, so the token could not be checked.
            var moved = stream.MoveNextAsync();
            var first = await bucket.NextContinue();
            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            await bucket.WhenClosed(first);
        }

        [Fact]
        public async Task Closes_Only_The_Open_Scans_When_The_Caller_Stops_Early()
        {
            var (collection, bucket) = CreateCollection();

            // The caller asked to stop, so it does not wait for the closes: DisposeAsync returns while
            // they are held.
            bucket.HoldCloses = true;

            var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator();

            // The first partition completes, so the server has already closed its scan.
            var moved = stream.MoveNextAsync().AsTask();
            await bucket.NextContinue();
            bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "a");
            Assert.True(await moved);

            moved = stream.MoveNextAsync().AsTask();
            var second = await bucket.NextContinue();
            bucket.CompleteContinue(ResponseStatus.RangeScanMore, "b");
            Assert.True(await moved);

            await stream.DisposeAsync();
            await bucket.WhenClosed(second);
            bucket.ReleaseCloses();

            // Wait for the whole cleanup, so a close of the completed scan cannot arrive after the assert.
            await collection.ScanCleanup;
            Assert.Equal([second], bucket.Closed);
        }

        [Fact]
        public async Task WithCancellation_Already_Cancelled_Throws_Before_A_Scan_Is_Created()
        {
            var (collection, bucket) = CreateCollection();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await using var stream = collection.ScanAsync(new RangeScan())
                .WithCancellation(cts.Token).GetAsyncEnumerator();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());

            Assert.Equal(cts.Token, canceled.CancellationToken);
            await collection.ScanCleanup;
            Assert.Empty(bucket.Created);
            Assert.Empty(bucket.Closed);
        }

        [Fact]
        public async Task WithCancellation_Leaves_The_Callers_Options_Unchanged()
        {
            var (collection, bucket) = CreateCollection();
            using var optionsCts = new CancellationTokenSource();
            using var cts = new CancellationTokenSource();
            var options = new ScanOptions().Token(optionsCts.Token);

            await using var stream = collection.ScanAsync(new RangeScan(), options)
                .WithCancellation(cts.Token).GetAsyncEnumerator();

            var moved = stream.MoveNextAsync();
            await bucket.NextContinue();
            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            Assert.Equal(optionsCts.Token, options.TokenValue);
            Assert.False(optionsCts.IsCancellationRequested);
        }

        // At the sampling limit the scan ends the stream itself, so the stream ends only after the scan
        // that still has documents is closed.
        [Fact]
        public async Task Sampling_Limit_Closes_The_Open_Scans_Before_The_Stream_Ends()
        {
            var (collection, bucket) = CreateCollection();
            bucket.HoldCloses = true;

            await using var stream = collection.ScanAsync(new SamplingScan(limit: 1)).GetAsyncEnumerator();

            var moved = stream.MoveNextAsync().AsTask();
            var first = await bucket.NextContinue();
            bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a", "b");
            Assert.True(await moved);
            Assert.Equal("a", stream.Current.Id);

            var ended = stream.MoveNextAsync().AsTask();
            await bucket.WhenClosed(first);
            Assert.False(ended.IsCompleted);

            bucket.ReleaseCloses();
            Assert.False(await ended);
            Assert.Equal([first], bucket.Closed);
        }

        [Fact]
        public async Task A_Failed_Close_Does_Not_Replace_The_Callers_Exception()
        {
            var (collection, bucket) = CreateCollection();
            bucket.FailCloses = true;
            using var cts = new CancellationTokenSource();

            await using var stream = collection.ScanAsync(new RangeScan())
                .WithCancellation(cts.Token).GetAsyncEnumerator();

            var moved = stream.MoveNextAsync();
            var first = await bucket.NextContinue();
            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            await bucket.WhenClosed(first);
        }

        // RFC 73 (KV Range Scan), Tracing Spans: range_scan > range_scan_partition > range_scan_create and
        // range_scan_continue.
        [Fact]
        public async Task Range_Scan_Creates_The_Rfc73_Span_Tree()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);
            var parent = tracer.RequestSpan("outer");
            var scanType = new RangeScan(ScanTerm.Inclusive("a"), ScanTerm.Exclusive("z"));

            await using (var stream = collection.ScanAsync(scanType, new ScanOptions().ParentSpan(parent)).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "a");
                Assert.True(await moved);

                moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "b");
                Assert.True(await moved);
                Assert.False(await stream.MoveNextAsync());
            }
            await collection.ScanCleanup;

            var scan = Assert.Single(tracer.Named("range_scan"));
            Assert.Same(parent, scan.Parent);
            Assert.Equal((uint)2, scan.Attributes["num_partitions"]);
            Assert.Equal("range", scan.Attributes["scan_type"]);
            Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("a")), scan.Attributes["from_term"]);
            Assert.Equal(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("z")), scan.Attributes["to_term"]);
            Assert.Equal(false, scan.Attributes["without_content"]);
            Assert.Equal("kv", scan.Attributes["db.couchbase.service"]);
            Assert.Equal("range_scan", scan.Attributes["db.operation"]);

            var partitions = tracer.Named("range_scan_partition");
            Assert.Equal(2, partitions.Count);
            Assert.All(partitions, p => Assert.Same(scan, p.Parent));
            Assert.Equal(new uint[] { 0, 1 }, partitions.Select(p => (uint)p.Attributes["partition_id"]).OrderBy(id => id));

            foreach (var partition in partitions)
            {
                var create = Assert.Single(tracer.ChildrenOf(partition, "range_scan_create"));
                Assert.Equal("range", create.Attributes["scan_type"]);
                Assert.Equal("a", create.Attributes["from_term"]);
                Assert.Equal("z", create.Attributes["to_term"]);
                Assert.Equal(false, create.Attributes["from_exclusive"]);
                Assert.Equal(true, create.Attributes["to_exclusive"]);

                var scanContinue = Assert.Single(tracer.ChildrenOf(partition, "range_scan_continue"));
                var endOrder = tracer.EndOrder.ToList();
                Assert.True(endOrder.IndexOf(create) < endOrder.IndexOf(partition), "create must end before its partition");
                Assert.True(endOrder.IndexOf(scanContinue) < endOrder.IndexOf(partition), "continue must end before its partition");
                Assert.Equal(FakeRangeScanId, scanContinue.Attributes["range_scan_id"]);
                Assert.Contains("item_limit", scanContinue.Attributes.Keys);
                Assert.Contains("byte_limit", scanContinue.Attributes.Keys);
                Assert.Contains("time_limit", scanContinue.Attributes.Keys);
            }

            Assert.Empty(tracer.Named("range_scan_cancel"));
            Assert.All(tracer.Spans.Where(x => x != parent), x => Assert.True(x.Ended, x.Name));
            Assert.Same(scan, tracer.EndOrder.Last());
        }

        // A partition with nothing in range answers the create with KeyNotFound. Its create span must still end
        // inside its partition span.
        [Fact]
        public async Task Empty_Partition_Create_Span_Ends_Inside_The_Partition_Span()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);
            bucket.EmptyPartitions.Add(0);

            await using (var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                Assert.Equal(1, await bucket.NextContinue());
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "a");
                Assert.True(await moved);
                Assert.False(await stream.MoveNextAsync());
            }
            await collection.ScanCleanup;

            var partition = tracer.Named("range_scan_partition").Single(p => (uint)p.Attributes["partition_id"] == 0);
            var create = Assert.Single(tracer.ChildrenOf(partition, "range_scan_create"));
            Assert.Empty(tracer.ChildrenOf(partition, "range_scan_continue"));
            var endOrder = tracer.EndOrder.ToList();
            Assert.True(endOrder.IndexOf(create) < endOrder.IndexOf(partition), "create must end before its partition");
        }

        // Before the fix, the scan loop skipped only RangeScanComplete partitions. An empty partition was created
        // again on each round, with a new partition span each time, while another partition read its batches.
        [Fact]
        public async Task Empty_Partition_Is_Created_Once_While_Another_Partition_Reads_Batches()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);
            bucket.EmptyPartitions.Add(0);

            await using (var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                Assert.Equal(1, await bucket.NextContinue());
                bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a");
                Assert.True(await moved);

                moved = stream.MoveNextAsync().AsTask();
                Assert.Equal(1, await bucket.NextContinue());
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "b");
                Assert.True(await moved);
                Assert.False(await stream.MoveNextAsync());
            }
            await collection.ScanCleanup;

            Assert.Single(bucket.Created, partitionId => partitionId == 0);
            var partition = Assert.Single(tracer.Named("range_scan_partition"), p => (uint)p.Attributes["partition_id"] == 0);
            Assert.Single(tracer.ChildrenOf(partition, "range_scan_create"));
        }

        // Before the fix, a create that returned RangeScanComplete left Status unset and Results null, so the
        // scan loop read a null Results.
        [Fact]
        public async Task Partition_Completed_By_The_Create_Is_Done()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);
            bucket.CompletedPartitions.Add(0);

            var ids = new List<string>();
            await using (var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                Assert.Equal(1, await NextContinueOrFailure(bucket, moved));
                bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a");
                Assert.True(await moved);
                ids.Add(stream.Current.Id);

                moved = stream.MoveNextAsync().AsTask();
                Assert.Equal(1, await NextContinueOrFailure(bucket, moved));
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "b");
                Assert.True(await moved);
                ids.Add(stream.Current.Id);
                Assert.False(await stream.MoveNextAsync());
            }
            await collection.ScanCleanup;

            Assert.Equal(new[] { "a", "b" }, ids);
            Assert.Single(bucket.Created, partitionId => partitionId == 0);
            Assert.DoesNotContain((short)0, bucket.Closed);
            Assert.Single(tracer.Named("range_scan_partition"), p => (uint)p.Attributes["partition_id"] == 0);
        }

        // The create and the first continue are separate requests: the create span must end before the
        // continue span starts.
        [Fact]
        public async Task Create_Span_Ends_Before_The_First_Continue_Starts()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);

            await using (var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "a");
                Assert.True(await moved);

                moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "b");
                Assert.True(await moved);
                Assert.False(await stream.MoveNextAsync());
            }
            await collection.ScanCleanup;

            var partitions = tracer.Named("range_scan_partition");
            Assert.Equal(2, partitions.Count);
            foreach (var partition in partitions)
            {
                var create = Assert.Single(tracer.ChildrenOf(partition, "range_scan_create"));
                var scanContinue = Assert.Single(tracer.ChildrenOf(partition, "range_scan_continue"));
                Assert.True(tracer.EndIndex(create) < tracer.StartIndex(scanContinue),
                    "create must end before the continue starts");
            }
        }

        // RFC 67: the outer span is Ok when the request completes and Error when it fails.
        [Fact]
        public async Task Range_Scan_Span_Is_Ok_When_The_Scan_Completes()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);
            bucket.EmptyPartitions.Add(0);

            await using (var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.CompleteContinue(ResponseStatus.RangeScanComplete, "a");
                Assert.True(await moved);
                Assert.False(await stream.MoveNextAsync());
            }
            await collection.ScanCleanup;

            Assert.Equal(RequestSpanStatusCode.Ok, Assert.Single(tracer.Named("range_scan")).Status);
        }

        [Fact]
        public async Task Range_Scan_Span_Is_Error_When_The_Scan_Fails()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);

            await using (var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.FailContinue(new UnambiguousTimeoutException("The fake bucket failed this continue."));
                await Assert.ThrowsAsync<UnambiguousTimeoutException>(async () => await moved);
            }
            await collection.ScanCleanup;

            var scan = Assert.Single(tracer.Named("range_scan"));
            Assert.Equal(RequestSpanStatusCode.Error, scan.Status);
            Assert.True(scan.Ended);
        }

        // A stop that the caller asks for is not a failure, and the scan did not complete.
        [Fact]
        public async Task Range_Scan_Span_Status_Is_Unset_When_The_Caller_Stops_Early()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);

            await using (var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a");
                Assert.True(await moved);
            }
            await collection.ScanCleanup;

            var scan = Assert.Single(tracer.Named("range_scan"));
            Assert.Equal(RequestSpanStatusCode.Unset, scan.Status);
            Assert.True(scan.Ended);
        }

        [Fact]
        public async Task Range_Scan_Span_Status_Is_Unset_When_The_Caller_Cancels()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);
            using var cts = new CancellationTokenSource();

            await using (var stream = collection.ScanAsync(new RangeScan()).WithCancellation(cts.Token).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync();
                await bucket.NextContinue();
                cts.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);
            }
            await collection.ScanCleanup;

            var scan = Assert.Single(tracer.Named("range_scan"));
            Assert.Equal(RequestSpanStatusCode.Unset, scan.Status);
            Assert.True(scan.Ended);
        }

        [Fact]
        public async Task Sampling_Scan_Span_Has_Limit_And_Seed()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);

            await using (var stream = collection.ScanAsync(new SamplingScan(limit: 1, seed: 7)).GetAsyncEnumerator())
            {
                var moved = stream.MoveNextAsync().AsTask();
                await bucket.NextContinue();
                bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a", "b");
                Assert.True(await moved);
                Assert.False(await stream.MoveNextAsync());
            }
            await collection.ScanCleanup;

            var scan = Assert.Single(tracer.Named("range_scan"));
            Assert.Equal(RequestSpanStatusCode.Ok, scan.Status);
            Assert.Equal("sampling", scan.Attributes["scan_type"]);
            Assert.Equal("1", scan.Attributes["limit"]);
            Assert.Equal("7", scan.Attributes["seed"]);
            Assert.DoesNotContain("from_term", scan.Attributes.Keys);

            var create = tracer.Named("range_scan_create").First();
            Assert.Equal("sampling", create.Attributes["scan_type"]);
            Assert.Equal("1", create.Attributes["limit"]);
            Assert.Equal("7", create.Attributes["seed"]);
        }

        // A scan closed early sends range_scan_cancel requests. The range_scan span ends only after
        // they finish, so it contains them.
        [Fact]
        public async Task Closing_Early_Adds_Cancel_Spans_Inside_The_Scan_Span()
        {
            var tracer = new RecordingTracer();
            var (collection, bucket) = CreateCollection(tracer);
            bucket.HoldCloses = true;

            var stream = collection.ScanAsync(new RangeScan()).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync().AsTask();
            var first = await bucket.NextContinue();
            bucket.CompleteContinue(ResponseStatus.RangeScanMore, "a");
            Assert.True(await moved);

            await stream.DisposeAsync();
            await bucket.WhenClosed(first);

            var scan = Assert.Single(tracer.Named("range_scan"));
            Assert.False(scan.Ended);

            bucket.ReleaseCloses();
            await collection.ScanCleanup;

            var cancel = Assert.Single(tracer.Named("range_scan_cancel"));
            var partition = Assert.IsType<RecordingSpan>(cancel.Parent);
            Assert.Equal("range_scan_partition", partition.Name);
            Assert.Equal((uint)first, partition.Attributes["partition_id"]);
            Assert.Equal(FakeRangeScanId, cancel.Attributes["range_scan_id"]);
            Assert.True(cancel.Ended);
            Assert.True(scan.Ended);
            Assert.Same(scan, tracer.EndOrder.Last());
        }

        /// <summary>
        /// Waits for the next RangeScanContinue. If the stream fails first, no continue is sent: rethrows the
        /// stream's error instead of waiting for ever.
        /// </summary>
        private static async Task<short> NextContinueOrFailure(PendingScanBucket bucket, Task<bool> moved)
        {
            var next = bucket.NextContinue();
            if (await Task.WhenAny(moved, next) == moved)
            {
                await moved;
                Assert.Fail("The stream moved without sending a RangeScanContinue.");
            }

            return await next;
        }

        private static (CouchbaseCollection Collection, PendingScanBucket Bucket) CreateCollection(IRequestTracer tracer = null)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json",
                InternalSerializationContext.Default.BucketConfig);

            // Two partitions keep the tests small. Collections are left unsupported so that no
            // collection ID is fetched.
            config.VBucketServerMap.VBucketMap = [[1, 0], [0, 1]];
            config.BucketCapabilities = [BucketCapabilities.RANGE_SCAN];

            var bucket = new PendingScanBucket(config);

            var collection = new CouchbaseCollection(bucket,
                new OperationConfigurator(new LegacyTranscoder(),
                    Mock.Of<IOperationCompressor>(),
                    new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                    new BestEffortRetryStrategy()),
                new Mock<ILogger<CouchbaseCollection>>().Object,
                new Mock<ILogger<GetResult>>().Object,
                new Redactor(RedactionLevel.None),
                CouchbaseCollection.DefaultCollectionName,
                Mock.Of<IScope>(scope => scope.IsDefaultScope == true && scope.Name == Scope.DefaultScopeName),
                tracer ?? new NoopRequestTracer(),
                NullFallbackTypeSerializerProvider.Instance,
                Mock.Of<IServiceProvider>());

            return (collection, bucket);
        }

        /// <summary>
        /// Creates every scan at once. Holds each RangeScanContinue until the test calls
        /// <see cref="CompleteContinue"/> or the read's token is cancelled. Records each RangeScanCancel.
        /// </summary>
        private sealed class PendingScanBucket(BucketConfig config)
            : CouchbaseCollectionZoneAwareTests.ZoneAwareFakeBucket(config, preferredServerGroup: null)
        {
            private static readonly ITypeTranscoder Transcoder = new LegacyTranscoder();

            private readonly ConcurrentQueue<(RangeScanContinue Operation, TaskCompletionSource<ResponseStatus> Read)> _continues = new();
            private readonly SemaphoreSlim _continueSent = new(0);
            private readonly ConcurrentDictionary<short, TaskCompletionSource<bool>> _closed = new();
            private (RangeScanContinue Operation, TaskCompletionSource<ResponseStatus> Read) _current;

            private readonly TaskCompletionSource<bool> _closesReleased =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// True to fail every RangeScanCancel.
            /// </summary>
            public bool FailCloses { get; set; }

            /// <summary>
            /// True to hold every RangeScanCancel until <see cref="ReleaseCloses"/>.
            /// </summary>
            public bool HoldCloses { get; set; }

            public void ReleaseCloses() => _closesReleased.TrySetResult(true);

            /// <summary>
            /// Partitions whose RangeScanCreate returns KeyNotFound, as for a partition with no documents in range.
            /// </summary>
            public HashSet<short> EmptyPartitions { get; } = new();

            /// <summary>
            /// Partitions whose RangeScanCreate returns RangeScanComplete, with no scan to read or close.
            /// </summary>
            public HashSet<short> CompletedPartitions { get; } = new();

            /// <summary>
            /// The partitions that a RangeScanCreate was sent for, in the order they were sent.
            /// </summary>
            public ConcurrentQueue<short> Created { get; } = new();

            /// <summary>
            /// The partitions that a RangeScanCancel was sent for, in the order they were sent.
            /// </summary>
            public ConcurrentQueue<short> Closed { get; } = new();

            public override Task<ResponseStatus> RecordAndFail(IOperation operation, CancellationTokenPair token)
            {
                switch (operation)
                {
                    case RangeScanCreate create:
                        Created.Enqueue((short) create.VBucketId!);
                        if (EmptyPartitions.Contains((short) create.VBucketId!))
                        {
                            return Task.FromResult(ResponseStatus.KeyNotFound);
                        }
                        if (CompletedPartitions.Contains((short) create.VBucketId!))
                        {
                            return Task.FromResult(ResponseStatus.RangeScanComplete);
                        }
                        // The scan's 16-byte UUID, which RangeScanContinue and RangeScanCancel send back.
                        var body = new byte[OperationHeader.Length + 16];
                        for (var i = 0; i < 16; i++)
                        {
                            body[OperationHeader.Length + i] = (byte) (0xA0 + i);
                        }
                        create.Read(new SlicedMemoryOwner<byte>(new TestMemoryOwner(body, 16)));
                        return Task.FromResult(ResponseStatus.Success);

                    case RangeScanContinue scanContinue:
                        var read = new TaskCompletionSource<ResponseStatus>();
                        var external = token.ExternalToken;
                        external.Register(() => read.TrySetCanceled(external));
                        _continues.Enqueue((scanContinue, read));
                        _continueSent.Release();
                        return read.Task;

                    case RangeScanCancel scanCancel:
                        var partitionId = (short) scanCancel.VBucketId!;
                        Closed.Enqueue(partitionId);
                        _closed.GetOrAdd(partitionId, NewSignal).TrySetResult(true);
                        return HoldCloses ? HeldClose() : CloseResult();

                        async Task<ResponseStatus> HeldClose()
                        {
                            await _closesReleased.Task.ConfigureAwait(false);
                            return await CloseResult().ConfigureAwait(false);
                        }

                        Task<ResponseStatus> CloseResult() => FailCloses
                            ? Task.FromException<ResponseStatus>(new TemporaryFailureException("The fake bucket failed this close."))
                            : Task.FromResult(ResponseStatus.Success);

                    default:
                        throw new NotSupportedException(operation.GetType().Name);
                }
            }

            /// <summary>
            /// Waits for the next RangeScanContinue and returns the partition it reads.
            /// </summary>
            public async Task<short> NextContinue()
            {
                await _continueSent.WaitAsync().ConfigureAwait(false);
                Assert.True(_continues.TryDequeue(out _current));
                return (short) _current.Operation.VBucketId!;
            }

            /// <summary>
            /// Finishes the RangeScanContinue that <see cref="NextContinue"/> returned, with a batch of
            /// the given document IDs.
            /// </summary>
            public void CompleteContinue(ResponseStatus status, params string[] ids)
            {
                _current.Operation.Content = ids.ToDictionary(id => id,
                    id => (IScanResult) new ScanResult(SlicedMemoryOwner<byte>.Empty, id, true, null, 0, 0,
                        OpCode.RangeScanContinue, Transcoder));
                _current.Read.SetResult(status);
            }

            /// <summary>
            /// Fails the RangeScanContinue that <see cref="NextContinue"/> returned.
            /// </summary>
            public void FailContinue(Exception exception) => _current.Read.SetException(exception);

            public Task WhenClosed(short partitionId) => _closed.GetOrAdd(partitionId, NewSignal).Task;

            private static TaskCompletionSource<bool> NewSignal(short _) =>
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// Records every span, its parent, its attributes and the order in which spans end.
        /// </summary>
        private sealed class RecordingTracer : IRequestTracer
        {
            private readonly object _lock = new();
            private readonly List<RecordingSpan> _spans = new();
            private readonly List<RecordingSpan> _endOrder = new();
            private readonly List<(bool Start, RecordingSpan Span)> _log = new();

            public IReadOnlyList<RecordingSpan> Spans { get { lock (_lock) { return _spans.ToList(); } } }

            public IReadOnlyList<RecordingSpan> EndOrder { get { lock (_lock) { return _endOrder.ToList(); } } }

            /// <summary>
            /// The position of the span's start among all span starts and ends.
            /// </summary>
            public int StartIndex(RecordingSpan span) { lock (_lock) { return _log.IndexOf((true, span)); } }

            /// <summary>
            /// The position of the span's end among all span starts and ends.
            /// </summary>
            public int EndIndex(RecordingSpan span) { lock (_lock) { return _log.IndexOf((false, span)); } }

            public List<RecordingSpan> Named(string name) => Spans.Where(s => s.Name == name).ToList();

            public List<RecordingSpan> ChildrenOf(RecordingSpan parent, string name) =>
                Spans.Where(s => s.Parent == parent && s.Name == name).ToList();

            public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
            {
                var span = new RecordingSpan(this, name, parentSpan);
                lock (_lock)
                {
                    _spans.Add(span);
                    _log.Add((true, span));
                }
                return span;
            }

            internal void Ended(RecordingSpan span)
            {
                lock (_lock)
                {
                    _endOrder.Add(span);
                    _log.Add((false, span));
                }
            }

            public IRequestTracer Start(TraceListener listener) => this;

            public void Dispose()
            {
            }
        }

        private sealed class RecordingSpan(RecordingTracer tracer, string name, IRequestSpan parent) : IRequestSpan
        {
            public string Name { get; } = name;

            public ConcurrentDictionary<string, object> Attributes { get; } = new();

            public bool Ended { get; private set; }

            public IRequestSpan Parent { get; set; } = parent;

            public bool CanWrite => true;

            public string Id { get; } = Guid.NewGuid().ToString();

            public uint? Duration => null;

            public IRequestSpan SetAttribute(string key, bool value) { Attributes[key] = value; return this; }

            public IRequestSpan SetAttribute(string key, string value) { Attributes[key] = value; return this; }

            public IRequestSpan SetAttribute(string key, uint value) { Attributes[key] = value; return this; }

            public IRequestSpan AddEvent(string eventName, DateTimeOffset? timestamp = null) => this;

            public RequestSpanStatusCode Status { get; private set; }

            public IRequestSpan SetStatus(RequestSpanStatusCode code) { Status = code; return this; }

            public IRequestSpan ChildSpan(string childName) => tracer.RequestSpan(childName, this);

            public void End() => Dispose();

            public void Dispose()
            {
                if (!Ended)
                {
                    Ended = true;
                    tracer.Ended(this);
                }
            }
        }

        /// <summary>
        /// A response buffer whose body starts after the header.
        /// </summary>
        private sealed class TestMemoryOwner(byte[] bytes, int bodyLength) : System.Buffers.IMemoryOwner<byte>
        {
            public Memory<byte> Memory { get; } = Init(bytes, bodyLength);

            public void Dispose()
            {
            }

            private static byte[] Init(byte[] bytes, int bodyLength)
            {
                bytes[0] = (byte) Magic.ClientResponse;
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), bodyLength);
                return bytes;
            }
        }
    }
}
