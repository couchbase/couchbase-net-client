using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    public class CouchbaseCollectionDurabilityTimeoutTests
    {
        private const string DocId = "thekey";

        [Fact]
        public async Task MutateIn_With_Durability_And_No_Timeout_Uses_KvDurabilityTimeout()
        {
            var (collection, bucket) = CouchbaseCollectionGetReplicaTests.CreateCollection();

            await Assert.ThrowsAnyAsync<Exception>(() => collection.MutateInAsync(DocId,
                specs => specs.Upsert("foo", "bar"),
                options => options.Durability(DurabilityLevel.Majority)));

            var op = Assert.IsType<MultiMutation<byte[]>>(Assert.Single(bucket.DispatchedOperations));
            Assert.True(op.HasDurability);
            Assert.Equal(bucket.Context.ClusterOptions.KvDurabilityTimeout, op.Timeout);
        }

        [Fact]
        public async Task MutateIn_Without_Durability_Uses_KvTimeout()
        {
            var (collection, bucket) = CouchbaseCollectionGetReplicaTests.CreateCollection();

            await Assert.ThrowsAnyAsync<Exception>(() => collection.MutateInAsync(DocId,
                specs => specs.Upsert("foo", "bar")));

            var op = Assert.IsType<MultiMutation<byte[]>>(Assert.Single(bucket.DispatchedOperations));
            Assert.False(op.HasDurability);
            Assert.Equal(bucket.Context.ClusterOptions.KvTimeout, op.Timeout);
        }

        [Fact]
        public async Task MutateIn_With_Durability_And_Explicit_Timeout_Uses_That_Timeout()
        {
            var (collection, bucket) = CouchbaseCollectionGetReplicaTests.CreateCollection();
            var timeout = TimeSpan.FromSeconds(7);

            await Assert.ThrowsAnyAsync<Exception>(() => collection.MutateInAsync(DocId,
                specs => specs.Upsert("foo", "bar"),
                options => options.Durability(DurabilityLevel.Majority).Timeout(timeout)));

            var op = Assert.IsType<MultiMutation<byte[]>>(Assert.Single(bucket.DispatchedOperations));
            Assert.Equal(timeout, op.Timeout);
        }
    }
}
