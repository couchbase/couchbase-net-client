using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Xunit;

namespace Couchbase.IntegrationTests
{
    [Collection("NonParallel")]
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
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                VerifyBucket(settings, result);

                // upsert
                settings.ConflictResolutionType = null; // not allowed to edit on existing bucket
                await bucketManager.UpdateBucketAsync(settings).ConfigureAwait(false);
                settings.ConflictResolutionType = ConflictResolutionType.Timestamp;

                // get all
                var allBuckets = await bucketManager.GetAllBucketsAsync().ConfigureAwait(false);
                VerifyBucket(settings, allBuckets.Single(x => x.Key == settings.Name).Value);

                // flush
                await bucketManager.FlushBucketAsync(settings.Name).ConfigureAwait(false);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
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

        [Fact]
        public async Task CreateEphemeralBucketWithDefaultEvictionPolicy()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Ephemeral,
                RamQuotaMB = 100
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(EvictionPolicyType.NoEviction, result.EvictionPolicy);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task CreateEphemeralBucketWithNruEvictionPolicy()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Ephemeral,
                RamQuotaMB = 100,
                EvictionPolicy = EvictionPolicyType.NotRecentlyUsed
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(EvictionPolicyType.NotRecentlyUsed, result.EvictionPolicy);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task CreateCouchbaseBucketWithFullEvictionPolicy()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 100,
                EvictionPolicy = EvictionPolicyType.ValueOnly
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(EvictionPolicyType.ValueOnly, result.EvictionPolicy);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task CreateCouchbaseBucketWithDefaultEvictionPolicy()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 100,
                EvictionPolicy = EvictionPolicyType.FullEviction
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(EvictionPolicyType.FullEviction, result.EvictionPolicy);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task CreateCouchbaseBucketWith_DurabilityMinLevel_None()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 100,
                EvictionPolicy = EvictionPolicyType.FullEviction
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(DurabilityLevel.None, result.DurabilityMinimumLevel);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task CreateCouchbaseBucketWith_DurabilityMinLevel_Majority()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 100,
                EvictionPolicy = EvictionPolicyType.FullEviction,
                DurabilityMinimumLevel = DurabilityLevel.Majority
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(DurabilityLevel.Majority, result.DurabilityMinimumLevel);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }
    }
}
