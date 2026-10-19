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

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);
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

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());
            await bucket.WhenClosed(first);
        }

        [Fact]
        public async Task Options_Token_Still_Cancels_The_Scan()
        {
            var (collection, bucket) = CreateCollection();
            using var cts = new CancellationTokenSource();

            await using var stream = collection.ScanAsync(new RangeScan(), new ScanOptions().Token(cts.Token))
                .GetAsyncEnumerator();

            var moved = stream.MoveNextAsync().AsTask();
            var first = await bucket.NextContinue();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);
            await bucket.WhenClosed(first);
        }

        [Fact]
        public async Task Closes_Only_The_Open_Scans_When_The_Caller_Stops_Early()
        {
            var (collection, bucket) = CreateCollection();

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

            // The scan sends every close before it awaits any of them, so when the close of the open
            // scan arrives, a close of the completed scan would have arrived too.
            await bucket.WhenClosed(second);
            Assert.Equal([second], bucket.Closed);
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

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);
            await bucket.WhenClosed(first);
        }

        private static (CouchbaseCollection Collection, PendingScanBucket Bucket) CreateCollection()
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
                new NoopRequestTracer(),
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

            /// <summary>
            /// True to fail every RangeScanCancel.
            /// </summary>
            public bool FailCloses { get; set; }

            /// <summary>
            /// The partitions that a RangeScanCancel was sent for, in the order they were sent.
            /// </summary>
            public ConcurrentQueue<short> Closed { get; } = new();

            public override Task<ResponseStatus> RecordAndFail(IOperation operation, CancellationTokenPair token)
            {
                switch (operation)
                {
                    case RangeScanCreate create:
                        // The scan's 16-byte UUID, which RangeScanContinue and RangeScanCancel send back.
                        create.Read(new SlicedMemoryOwner<byte>(new TestMemoryOwner(new byte[OperationHeader.Length + 16], 16)));
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
                        return FailCloses
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

            public Task WhenClosed(short partitionId) => _closed.GetOrAdd(partitionId, NewSignal).Task;

            private static TaskCompletionSource<bool> NewSignal(short _) =>
                new(TaskCreationOptions.RunContinuationsAsynchronously);
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
