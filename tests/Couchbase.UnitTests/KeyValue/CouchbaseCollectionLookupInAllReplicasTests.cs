using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    /// <summary>
    /// LookupInAllReplicasAsync cancellation (NCBC-4365). Every lookup stays pending until the test
    /// finishes it or its token is cancelled, so the result does not depend on the scheduler. There is
    /// no wall-clock bound: a lookup that is never cancelled makes the test wait, and CI's --blame-hang
    /// fails a hung run and keeps a dump.
    /// </summary>
    public class CouchbaseCollectionLookupInAllReplicasTests
    {
        private const string DocId = "thekey";

        private static readonly LookupInSpec[] Specs = [LookupInSpec.Get("name")];

        [Fact]
        public async Task WithCancellation_Cancels_The_Pending_Lookups()
        {
            var (collection, bucket) = CouchbaseCollectionGetAllReplicasStreamTests.CreateCollection(preferredServerGroup: null);
            using var cts = new CancellationTokenSource();

            await using var stream = collection.LookupInAllReplicasAsync(DocId, Specs)
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
            var (collection, bucket) = CouchbaseCollectionGetAllReplicasStreamTests.CreateCollection(preferredServerGroup: null);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await using var stream = collection.LookupInAllReplicasAsync(DocId, Specs)
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
            var (collection, bucket) = CouchbaseCollectionGetAllReplicasStreamTests.CreateCollection(preferredServerGroup: null);
            using var optionsCts = new CancellationTokenSource();
            using var cts = new CancellationTokenSource();
            var options = new LookupInAllReplicasOptions().CancellationToken(optionsCts.Token);

            await using var stream = collection.LookupInAllReplicasAsync(DocId, Specs, options)
                .WithCancellation(cts.Token).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            Assert.Equal(optionsCts.Token, options.TokenValue);
            Assert.False(optionsCts.IsCancellationRequested);
        }

        [Fact]
        public async Task Options_Token_Still_Cancels_The_Pending_Lookups()
        {
            var (collection, bucket) = CouchbaseCollectionGetAllReplicasStreamTests.CreateCollection(preferredServerGroup: null);
            using var cts = new CancellationTokenSource();

            await using var stream = collection.LookupInAllReplicasAsync(DocId, Specs,
                new LookupInAllReplicasOptions().CancellationToken(cts.Token)).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            cts.Cancel();

            var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);

            Assert.Equal(cts.Token, canceled.CancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bucket.Read(null));
        }

        [Fact]
        public async Task Cancels_The_Lookups_Still_In_Progress_When_The_Caller_Stops_Early()
        {
            var (collection, bucket) = CouchbaseCollectionGetAllReplicasStreamTests.CreateCollection(preferredServerGroup: null);

            var stream = collection.LookupInAllReplicasAsync(DocId, Specs).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            // The stream yields the lookups in the order it sent them, the primary first.
            bucket.SucceedLookup(null);
            Assert.True(await moved);
            Assert.Equal(CouchbaseCollectionGetAllReplicasStreamTests.PendingReadsBucket.PrimaryCas, stream.Current.Cas);
            using var yielded = stream.Current;

            await stream.DisposeAsync();

            foreach (var replicaIndex in new short?[] { 0, 2, 3 })
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bucket.Read(replicaIndex));
            }
        }

        [Fact]
        public async Task Disposes_The_Results_It_Did_Not_Yield_When_The_Caller_Stops_Early()
        {
            // The stream cancels the lookups it leaves behind, so these lookups ignore their token, as
            // a lookup does when its response is already on the way. Then replica 2 can still finish.
            var (collection, bucket) = CouchbaseCollectionGetAllReplicasStreamTests.CreateCollection(
                preferredServerGroup: null, honoursCancellation: false);

            var stream = collection.LookupInAllReplicasAsync(DocId, Specs).GetAsyncEnumerator();
            var moved = stream.MoveNextAsync();

            bucket.SucceedLookup(null);
            Assert.True(await moved);
            using var yielded = stream.Current;

            // Replica 0 finishes before the caller stops and replica 2 after. The stream disposes each
            // result it did not yield whenever its lookup finishes, so the test awaits the disposal.
            bucket.SucceedLookup(0);
            bucket.Fail(3);
            await stream.DisposeAsync();
            await bucket.WhenBufferDisposed(0);

            bucket.SucceedLookup(2);
            await bucket.WhenBufferDisposed(2);

            Assert.False(bucket.WhenBufferDisposed(null).IsCompleted);
        }
    }
}
