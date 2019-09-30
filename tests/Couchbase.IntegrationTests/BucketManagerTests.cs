using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management;
using Couchbase.Management.Buckets;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class BucketManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public BucketManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_BucketManager()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var settings = new BucketSettings
            {
                Name = "mike_test",
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 100,
                MaxTtl = 100,
                ReplicaIndexes = true,
                NumReplicas = 1,
                FlushEnabled = true,
                CompressionMode = CompressionMode.Active,
                ConflictResolutionType = ConflictResolutionType.Timestamp,
                EjectionMethod = EvictionPolicyType.FullEviction
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name);
                VerifyBucket(settings, result);

                // upsert
                settings.ConflictResolutionType = null; // not allowed to edit on existing bucket
                await bucketManager.UpsertBucketAsync(settings);
                settings.ConflictResolutionType = ConflictResolutionType.Timestamp;

                // get all
                var allBuckets = await bucketManager.GetAllBucketsAsync();
                VerifyBucket(settings, allBuckets.Single(x => x.Key == settings.Name).Value);

                // flush
                await bucketManager.FlushBucketAsync(settings.Name);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name);
            }
        }

        private static void VerifyBucket(BucketSettings expected, BucketSettings actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.BucketType, actual.BucketType);
            var ramQuotaInBytes = expected.RamQuotaMB * 1024 * 1024; // returned value is in bytes, not mb :|
            Assert.Equal(ramQuotaInBytes, actual.RamQuotaMB);
            Assert.Equal(expected.MaxTtl, actual.MaxTtl);
            Assert.Equal(expected.ReplicaIndexes, actual.ReplicaIndexes);
            Assert.Equal(expected.NumReplicas, actual.NumReplicas);
            Assert.Equal(expected.FlushEnabled, actual.FlushEnabled);
            Assert.Equal(expected.CompressionMode, actual.CompressionMode);
            Assert.Equal(expected.ConflictResolutionType, actual.ConflictResolutionType);
            Assert.Equal(expected.EjectionMethod, actual.EjectionMethod);
        }
    }
}
