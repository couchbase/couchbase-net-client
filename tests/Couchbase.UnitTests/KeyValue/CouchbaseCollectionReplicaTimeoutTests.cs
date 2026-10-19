using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.UnitTests.KeyValue
{
    /// <summary>
    /// The timeout of GetAnyReplica and GetAllReplicas (NCBC-4364) applies to each read, of the
    /// active and of every replica, and falls back to <see cref="ClusterOptions.KvTimeout"/>.
    /// </summary>
    public class CouchbaseCollectionReplicaTimeoutTests
    {
        private const string DocId = "thekey";

        // The primary is node 1, and the replicas are nodes 0, 2 and 3.
        private static readonly short[] VBucketMapRow = [1, 0, 2, 3];

        private static readonly TimeSpan CustomTimeout = TimeSpan.FromSeconds(7);

        [Fact]
        public async Task GetAnyReplica_Applies_The_Timeout_To_Every_Read()
        {
            var (collection, bucket) = CreateCollection();

            await Assert.ThrowsAsync<DocumentUnretrievableException>(() =>
                collection.GetAnyReplicaAsync(DocId, new GetAnyReplicaOptions().Timeout(CustomTimeout)));

            AssertEveryReadUses(bucket, CustomTimeout);
        }

        [Fact]
        public async Task GetAnyReplica_Without_Timeout_Uses_KvTimeout()
        {
            var (collection, bucket) = CreateCollection();

            await Assert.ThrowsAsync<DocumentUnretrievableException>(() => collection.GetAnyReplicaAsync(DocId));

            AssertEveryReadUses(bucket, bucket.Context.ClusterOptions.KvTimeout);
        }

        [Fact]
        public async Task GetAllReplicas_Applies_The_Timeout_To_Every_Read()
        {
            var (collection, bucket) = CreateCollection();

            await ObserveAsync(collection.GetAllReplicasAsync(DocId, new GetAllReplicasOptions().Timeout(CustomTimeout)));

            AssertEveryReadUses(bucket, CustomTimeout);
        }

        [Fact]
        public async Task GetAllReplicas_Without_Timeout_Uses_KvTimeout()
        {
            var (collection, bucket) = CreateCollection();

            await ObserveAsync(collection.GetAllReplicasAsync(DocId));

            AssertEveryReadUses(bucket, bucket.Context.ClusterOptions.KvTimeout);
        }

        [Fact]
        public async Task GetAllReplicasStream_Applies_The_Timeout_To_Every_Read()
        {
            var (collection, bucket) = CreateCollection();

            await foreach (var _ in collection.GetAllReplicasStreamAsync(DocId,
                               new GetAllReplicasOptions().Timeout(CustomTimeout)))
            {
                Assert.Fail("Every read was expected to fail.");
            }

            AssertEveryReadUses(bucket, CustomTimeout);
        }

        [Fact]
        public async Task GetAllReplicasStream_Without_Timeout_Uses_KvTimeout()
        {
            var (collection, bucket) = CreateCollection();

            await foreach (var _ in collection.GetAllReplicasStreamAsync(DocId))
            {
                Assert.Fail("Every read was expected to fail.");
            }

            AssertEveryReadUses(bucket, bucket.Context.ClusterOptions.KvTimeout);
        }

        [Fact]
        public void GetAllReplicasOptions_Timeout_Reaches_ReadOnly_And_ITimeoutOptions()
        {
            var options = new GetAllReplicasOptions().Timeout(CustomTimeout);

            Assert.Equal(CustomTimeout, ((ITimeoutOptions) options).Timeout);
            Assert.Equal(CustomTimeout, options.AsReadOnly().Timeout);
            Assert.Null(GetAllReplicasOptions.DefaultReadOnly.Timeout);
        }

        [Fact]
        public void GetAnyReplicaOptions_Timeout_Reaches_ReadOnly_And_ITimeoutOptions()
        {
            var options = new GetAnyReplicaOptions().Timeout(CustomTimeout);

            Assert.Equal(CustomTimeout, ((ITimeoutOptions) options).Timeout);
            Assert.Equal(CustomTimeout, options.AsReadOnly().Timeout);
            Assert.Null(GetAnyReplicaOptions.DefaultReadOnly.Timeout);
        }

        [Fact]
        public void Timeout_On_Default_Returns_A_Copy()
        {
            var allReplicas = GetAllReplicasOptions.Default.Timeout(CustomTimeout);
            var anyReplica = GetAnyReplicaOptions.Default.Timeout(CustomTimeout);

            Assert.NotSame(GetAllReplicasOptions.Default, allReplicas);
            Assert.Equal(CustomTimeout, ((ITimeoutOptions) allReplicas).Timeout);
            Assert.Null(((ITimeoutOptions) GetAllReplicasOptions.Default).Timeout);

            Assert.NotSame(GetAnyReplicaOptions.Default, anyReplica);
            Assert.Equal(CustomTimeout, ((ITimeoutOptions) anyReplica).Timeout);
            Assert.Null(((ITimeoutOptions) GetAnyReplicaOptions.Default).Timeout);
        }

        // The members that existed before Timeout was added are kept, so that callers compiled
        // against them still bind.
        [Fact]
        public void ReadOnly_Keeps_The_Members_Without_Timeout()
        {
            var options = new GetAllReplicasOptions().Timeout(CustomTimeout);

            var readOnly = new GetAllReplicasOptions.ReadOnly(default, null, null, null, default);
            Assert.Null(readOnly.Timeout);

            options.AsReadOnly().Deconstruct(out _, out _, out _, out _, out _);
            options.Deconstruct(out _, out _, out _, out _, out _);

            var anyReadOnly = new GetAnyReplicaOptions.ReadOnly(default, null, null, null, default);
            Assert.Null(anyReadOnly.Timeout);
        }

        private static void AssertEveryReadUses(CouchbaseCollectionZoneAwareTests.ZoneAwareFakeBucket bucket,
            TimeSpan timeout)
        {
            // The primary and the three replicas.
            Assert.Equal(4, bucket.DispatchedTimeouts.Count);
            Assert.All(bucket.DispatchedTimeouts, dispatched => Assert.Equal(timeout, dispatched));
        }

        private static async Task ObserveAsync(IEnumerable<Task<IGetReplicaResult>> reads)
        {
            foreach (var read in reads.ToList())
            {
                await Assert.ThrowsAnyAsync<Exception>(() => read);
            }
        }

        private static (CouchbaseCollection Collection, CouchbaseCollectionZoneAwareTests.ZoneAwareFakeBucket Bucket)
            CreateCollection() =>
            CouchbaseCollectionZoneAwareTests.CreateCollection(preferredServerGroup: null, VBucketMapRow);
    }
}
