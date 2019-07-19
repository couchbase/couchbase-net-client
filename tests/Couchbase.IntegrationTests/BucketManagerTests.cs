using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management;
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
                RamQuota = 100,
                AuthType = AuthType.Sasl,
                Password = "pa$$w0rd",
                MaxTtl = 100,
                ReplicaIndexes = true,
                ReplicaCount = 1,
                FlushEnabled = true,
                CompressionMode = CompressionMode.Active,
                ConflictResolutionType = ConflictResolutionType.Timestamp,
                EvictionPolicyType = EvictionPolicyType.FullEviction
            };

            try
            {
                // create
                await bucketManager.CreateAsync(settings);

                // get
                var result = await bucketManager.GetAsync(settings.Name);
                VerifyBucket(settings, result);

                // upsert
                settings.ConflictResolutionType = null; // not allowed to edit on existing bucket
                await bucketManager.UpsertAsync(settings);
                settings.ConflictResolutionType = ConflictResolutionType.Timestamp;

                // get all
                var allBuckets = await bucketManager.GetAllAsync();
                VerifyBucket(settings, allBuckets.Single(x => x.Key == settings.Name).Value);

                // flush
                await bucketManager.FlushAsync(settings.Name);
            }
            finally
            {
                // drop
                await bucketManager.DropAsync(settings.Name);
            }
        }

        private static void VerifyBucket(BucketSettings expected, BucketSettings actual)
        {
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.BucketType, actual.BucketType);
            var ramQuotaInBytes = expected.RamQuota * 1024 * 1024; // returned value is in bytes, not mb :|
            Assert.Equal(ramQuotaInBytes, actual.RamQuota);
            Assert.Equal(expected.AuthType, actual.AuthType);
            Assert.Equal(expected.MaxTtl, actual.MaxTtl);
            Assert.Equal(expected.ReplicaIndexes, actual.ReplicaIndexes);
            Assert.Equal(expected.ReplicaCount, actual.ReplicaCount);
            Assert.Equal(expected.FlushEnabled, actual.FlushEnabled);
            Assert.Equal(expected.CompressionMode, actual.CompressionMode);
            Assert.Equal(expected.ConflictResolutionType, actual.ConflictResolutionType);
            Assert.Equal(expected.EvictionPolicyType, actual.EvictionPolicyType);
        }
    }
}
