using Couchbase.Client.Transactions.Support;
using Couchbase.Core;
using Couchbase.UnitTests.KeyValue;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions
{
    /// <summary>
    /// The transactions layer used to pass <c>Bucket.SupportsCollections</c> as the PreserveTtl flag,
    /// with a comment calling it "a proxy for supporting TTLs". They are unrelated capabilities: every
    /// node in a mixed-version cluster can support collections while some node has not negotiated
    /// PreserveTtl, and CouchbaseCollection throws FeatureNotAvailableException for exactly that
    /// combination - so the proxy had transactions asking for something the cluster would refuse.
    /// </summary>
    public class PreserveTtlCapabilityTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Capability_Comes_From_The_Cluster_Not_From_Collections(bool clusterSupportsPreserveTtl)
        {
            var bucket = FakeBucket(supportsPreserveTtl: clusterSupportsPreserveTtl);
            var collection = CollectionOn(bucket);

            Assert.Equal(clusterSupportsPreserveTtl, collection.SupportsPreserveTtl());
        }

        /// <summary>
        /// Collections support must not be what decides this. Here the bucket supports collections
        /// while the cluster cannot preserve expiry, which is the mixed-version case the old proxy got
        /// wrong.
        /// </summary>
        [Fact]
        public void Collections_Support_Does_Not_Imply_PreserveTtl()
        {
            var bucket = FakeBucket(supportsPreserveTtl: false);
            var collection = CollectionOn(bucket);

            Assert.True(bucket.SupportsCollections);
            Assert.False(collection.SupportsPreserveTtl());
        }

        /// <summary>
        /// Protostellar buckets are not a <see cref="BucketBase"/> and have no ClusterContext to ask,
        /// so they keep the old proxy rather than silently losing TTL preservation.
        /// </summary>
        [Fact]
        public void A_bucket_without_a_cluster_context_falls_back_to_collections_support()
        {
            var bucket = Mock.Of<IBucket>(b => b.SupportsCollections == true);
            var collection = CollectionOn(bucket);

            Assert.True(collection.SupportsPreserveTtl());
        }

        private static ICouchbaseCollection CollectionOn(IBucket bucket) =>
            Mock.Of<ICouchbaseCollection>(collection =>
                collection.Scope == Mock.Of<IScope>(scope => scope.Bucket == bucket));

        private static CouchbaseCollectionCollectionIdTests.CidFakeBucket FakeBucket(bool supportsPreserveTtl)
        {
            var bucket = CouchbaseCollectionCollectionIdTests.CreateBucketWithCollections();
            bucket.Context.SupportsPreserveTtl = supportsPreserveTtl;
            return bucket;
        }
    }
}
