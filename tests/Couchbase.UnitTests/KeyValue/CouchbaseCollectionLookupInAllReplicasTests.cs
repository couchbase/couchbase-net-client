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

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);
            foreach (var replicaIndex in new short?[] { null, 0, 2, 3 })
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => bucket.Read(replicaIndex));
            }
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

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await moved);
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
    }
}
