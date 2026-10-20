using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.KeyValue.ZoneAware;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    /// <summary>
    /// GetAllReplicasStreamAsync (NCBC-3625): results arrive in the order the reads finish, failed
    /// reads are left out, and errors for the whole call are thrown before enumeration. Every read
    /// stays pending until the test finishes it, so the order is set by the test, not the scheduler.
    /// An ordering regression makes a test wait forever for a read it has not finished; there is no
    /// wall-clock bound, because CI's --blame-hang fails a hung run and keeps a dump.
    /// </summary>
    public class CouchbaseCollectionGetAllReplicasStreamTests
    {
        private const string DocId = "thekey";

        // The primary is node 1, and the replicas are nodes 0, 2 and 3. Nodes 1 and 0 are in group_1.
        private static readonly short[] VBucketMapRow = [1, 0, 2, 3];

        [Fact]
        public async Task Yields_Results_In_The_Order_The_Reads_Finish()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);

            await using var stream = collection.GetAllReplicasStreamAsync(DocId).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            // Every read is sent on the first MoveNextAsync, the primary first.
            Assert.Equal([null, (short) 0, (short) 2, (short) 3], bucket.DispatchedReplicaIndexes);

            bucket.Succeed(3);
            Assert.True(await moved);
            Assert.Equal(3UL, stream.Current.Cas);
            Assert.False(stream.Current.IsActive);

            bucket.Succeed(null);
            Assert.True(await stream.MoveNextAsync());
            Assert.Equal(PendingReadsBucket.PrimaryCas, stream.Current.Cas);
            Assert.True(stream.Current.IsActive);

            bucket.Succeed(0);
            Assert.True(await stream.MoveNextAsync());
            Assert.Equal(0UL, stream.Current.Cas);

            bucket.Succeed(2);
            Assert.True(await stream.MoveNextAsync());
            Assert.Equal(2UL, stream.Current.Cas);

            Assert.False(await stream.MoveNextAsync());
        }

        [Fact]
        public async Task Leaves_Failed_Reads_Out_Of_The_Stream()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);

            await using var stream = collection.GetAllReplicasStreamAsync(DocId).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            bucket.Fail(null);
            bucket.Fail(0);
            bucket.Succeed(2);
            Assert.True(await moved);
            Assert.Equal(2UL, stream.Current.Cas);

            bucket.Fail(3);
            Assert.False(await stream.MoveNextAsync());
        }

        [Fact]
        public async Task Is_Empty_When_Every_Read_Fails()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);

            await using var stream = collection.GetAllReplicasStreamAsync(DocId).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            bucket.Fail(null);
            bucket.Fail(0);
            bucket.Fail(2);
            bucket.Fail(3);

            Assert.False(await moved);
        }

        [Fact]
        public void Sends_Nothing_Until_Enumerated()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);

            _ = collection.GetAllReplicasStreamAsync(DocId);

            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        // An unusable server group fails the whole call, so it must throw at the call site rather
        // than produce an empty stream.
        [Fact]
        public void Without_PreferredServerGroup_Throws_Before_Enumeration()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);

            Assert.Throws<DocumentUnretrievableException>(() => collection.GetAllReplicasStreamAsync(DocId,
                new GetAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroup)));
            Assert.Empty(bucket.DispatchedReplicaIndexes);
        }

        [Fact]
        public async Task SelectedServerGroup_Reads_Only_The_Preferred_Group()
        {
            var (collection, bucket) = CreateCollection("group_1");

            await using var stream = collection.GetAllReplicasStreamAsync(DocId,
                    new GetAllReplicasOptions().ReadPreference(InternalReadPreference.SelectedServerGroup))
                .GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            // Replica node 0 then primary node 1. Nodes 2 and 3 are not read.
            Assert.Equal([(short) 0, null], bucket.DispatchedReplicaIndexes);

            bucket.Succeed(null);
            Assert.True(await moved);
            Assert.True(stream.Current.IsActive);

            bucket.Succeed(0);
            Assert.True(await stream.MoveNextAsync());
            Assert.False(await stream.MoveNextAsync());
        }

        [Fact]
        public async Task Disposes_The_Results_It_Did_Not_Yield_When_The_Caller_Stops_Early()
        {
            // The stream cancels the reads it leaves behind, so these reads ignore their token, as a
            // read does when its response is already on the way. Then replica 3 can still finish.
            var (collection, bucket) = CreateCollection(preferredServerGroup: null, honoursCancellation: false);

            var stream = collection.GetAllReplicasStreamAsync(DocId).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            bucket.Succeed(0);
            Assert.True(await moved);
            using var yielded = stream.Current;

            // Replica 2 finishes before the caller stops and replica 3 after. The stream disposes each
            // result it did not yield whenever its read finishes, so the test awaits the disposal. It
            // does not check it at once: a read can reach the stream after Succeed returns, because
            // await continuations are not always run inline (seen on net48).
            bucket.Succeed(2);
            bucket.Fail(null);
            await stream.DisposeAsync();
            await bucket.WhenBufferDisposed(2);

            bucket.Succeed(3);
            await bucket.WhenBufferDisposed(3);

            Assert.False(bucket.WhenBufferDisposed(0).IsCompleted);
        }

        [Fact]
        public async Task WithCancellation_Cancels_The_Pending_Reads()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);
            using var cts = new CancellationTokenSource();

            await using var stream = collection.GetAllReplicasStreamAsync(DocId)
                .WithCancellation(cts.Token).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            foreach (var replicaIndex in new short?[] { null, 0, 2, 3 })
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bucket.Read(replicaIndex));
            }
        }

        [Fact]
        public async Task WithCancellation_Already_Cancelled_Throws()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await using var stream = collection.GetAllReplicasStreamAsync(DocId)
                .WithCancellation(cts.Token).GetAsyncEnumerator();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());

            Assert.Equal(cts.Token, canceled.CancellationToken);
            foreach (var replicaIndex in new short?[] { null, 0, 2, 3 })
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bucket.Read(replicaIndex));
            }
        }

        [Fact]
        public async Task WithCancellation_Leaves_The_Callers_Options_Unchanged()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);
            using var optionsCts = new CancellationTokenSource();
            using var cts = new CancellationTokenSource();
            var options = new GetAllReplicasOptions().CancellationToken(optionsCts.Token);

            await using var stream = collection.GetAllReplicasStreamAsync(DocId, options)
                .WithCancellation(cts.Token).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            Assert.Equal(optionsCts.Token, options.TokenValue);
            Assert.False(optionsCts.IsCancellationRequested);
        }

        [Fact]
        public async Task Options_Token_Still_Cancels_The_Pending_Reads()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);
            using var cts = new CancellationTokenSource();

            await using var stream = collection.GetAllReplicasStreamAsync(DocId,
                new GetAllReplicasOptions().CancellationToken(cts.Token)).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bucket.Read(null));
        }

        [Fact]
        public async Task Cancels_The_Reads_Still_In_Progress_When_The_Caller_Stops_Early()
        {
            var (collection, bucket) = CreateCollection(preferredServerGroup: null);

            var stream = collection.GetAllReplicasStreamAsync(DocId).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            bucket.Succeed(0);
            Assert.True(await moved);
            using var yielded = stream.Current;

            await stream.DisposeAsync();

            foreach (var replicaIndex in new short?[] { null, 2, 3 })
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bucket.Read(replicaIndex));
            }
        }

        internal static (CouchbaseCollection Collection, PendingReadsBucket Bucket) CreateCollection(
            string preferredServerGroup, bool honoursCancellation = true)
        {
            var config = ResourceHelper.ReadResource(@"Documents\Configs\configWithReplicasAndServerGroups.json",
                InternalSerializationContext.Default.BucketConfig);

            // Every key maps to the same topology, and collections are left unsupported so that no
            // collection ID is fetched.
            config.VBucketServerMap.VBucketMap = Enumerable.Repeat(VBucketMapRow, 1024).ToArray();

            // For the LookupInAllReplicas tests, which use this collection too.
            config.BucketCapabilities = [BucketCapabilities.SUBDOC_REPLICA_READ];

            var bucket = new PendingReadsBucket(config, preferredServerGroup, honoursCancellation);

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
        /// Holds every read until the test calls <see cref="Succeed"/>, <see cref="SucceedLookup"/> or
        /// <see cref="Fail"/>, or until the read's token is cancelled. The read can reach the stream after
        /// they return, so a test awaits what it checks. A successful read returns the replica index as
        /// its CAS, so a result shows which node it came from.
        /// </summary>
        /// <param name="honoursCancellation">
        /// False to ignore the read's token, as a read does when its response is already on the way.
        /// </param>
        internal sealed class PendingReadsBucket(BucketConfig config, string preferredServerGroup,
            bool honoursCancellation)
            : CouchbaseCollectionZoneAwareTests.ZoneAwareFakeBucket(config, preferredServerGroup)
        {
            public const ulong PrimaryCas = 100;

            private readonly Dictionary<short, (IOperation Operation, TaskCompletionSource<ResponseStatus> Read)> _reads = new();
            private readonly Dictionary<short, TrackedBuffer> _buffers = new();

            public override Task<ResponseStatus> RecordAndFail(IOperation operation, CancellationTokenPair token)
            {
                DispatchedReplicaIndexes.Add(operation.ReplicaIdx);

                var read = new TaskCompletionSource<ResponseStatus>();
                _reads.Add(Key(operation.ReplicaIdx), (operation, read));

                // Only the external token: RetryOrchestrator throws OperationCanceledException for it,
                // and turns a cancel of the internal (timeout) token into a timeout.
                if (honoursCancellation)
                {
                    var external = token.ExternalToken;
                    external.Register(() => read.TrySetCanceled(external));
                }

                return read.Task;
            }

            /// <summary>
            /// The read sent to this replica, null for the primary. Await it to see that it was cancelled.
            /// </summary>
            public Task<ResponseStatus> Read(short? replicaIndex) => _reads[Key(replicaIndex)].Read.Task;

            public void Succeed(short? replicaIndex) =>
                Succeed(replicaIndex, SuccessResponse(CasFor(replicaIndex)));

            /// <summary>
            /// Finishes a sub-document lookup of one spec, which finds the value {}.
            /// </summary>
            public void SucceedLookup(short? replicaIndex) =>
                Succeed(replicaIndex, LookupSuccessResponse(CasFor(replicaIndex)));

            private void Succeed(short? replicaIndex, byte[] response)
            {
                var (operation, read) = _reads[Key(replicaIndex)];

                var buffer = new TrackedBuffer(response);
                _buffers.Add(Key(replicaIndex), buffer);
                ((OperationBase) operation).Read(new SlicedMemoryOwner<byte>(buffer));
                read.SetResult(ResponseStatus.Success);
            }

            public Task<bool> WhenBufferDisposed(short? replicaIndex) => _buffers[Key(replicaIndex)].Disposed;

            private static ulong CasFor(short? replicaIndex) => replicaIndex is { } index ? (ulong) index : PrimaryCas;

            public void Fail(short? replicaIndex) =>
                _reads[Key(replicaIndex)].Read.SetException(
                    new TemporaryFailureException("The fake bucket failed this read."));

            // Dictionary keys cannot be null, so the primary is stored as -1.
            private static short Key(short? replicaIndex) => replicaIndex ?? -1;

            // A success response with 4 bytes of flags as extras and a JSON body of {}.
            private static byte[] SuccessResponse(ulong cas)
            {
                var packet = new byte[OperationHeader.Length + 4 + 2];
                packet[0] = (byte) Magic.ClientResponse;
                packet[4] = 4;
                packet[5] = (byte) DataType.Json;
                BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(8), 4 + 2);
                BinaryPrimitives.WriteUInt64BigEndian(packet.AsSpan(16), cas);
                packet[packet.Length - 2] = (byte) '{';
                packet[packet.Length - 1] = (byte) '}';
                return packet;
            }

            // A multi-lookup success response with no extras and one spec result: a status of
            // success, a value length of 2 and the value {}.
            private static byte[] LookupSuccessResponse(ulong cas)
            {
                var packet = new byte[OperationHeader.Length + 2 + 4 + 2];
                packet[0] = (byte) Magic.ClientResponse;
                packet[1] = (byte) OpCode.MultiLookup;
                packet[5] = (byte) DataType.Json;
                BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(8), 2 + 4 + 2);
                BinaryPrimitives.WriteUInt64BigEndian(packet.AsSpan(16), cas);
                BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(OperationHeader.Length + 2), 2);
                packet[packet.Length - 2] = (byte) '{';
                packet[packet.Length - 1] = (byte) '}';
                return packet;
            }
        }

        /// <summary>
        /// A response buffer that records whether it was given back.
        /// </summary>
        private sealed class TrackedBuffer(byte[] bytes) : IMemoryOwner<byte>
        {
            private readonly TaskCompletionSource<bool> _disposed =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<bool> Disposed => _disposed.Task;

            public Memory<byte> Memory => bytes;

            public void Dispose() => _disposed.TrySetResult(true);
        }
    }
}
