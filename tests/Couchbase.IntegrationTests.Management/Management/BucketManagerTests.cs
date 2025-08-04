using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Management
{
    [Collection(NonParallelDefinition.Name)]
    public class BucketManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public BucketManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task CreateAndDropCouchbaseBucket()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucketName = nameof(BucketManagerTests) + "_" + nameof(CreateAndDropCouchbaseBucket);

            try
            {
                await cluster.Buckets.CreateBucketAsync(new BucketSettings
                {
                    BucketType = BucketType.Couchbase,
                    Name = bucketName,
                    NumReplicas = 0,
                    RamQuotaMB = 100
                }).ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);
            }
            catch (BucketExistsException e)
            {
                _outputHelper.WriteLine("Bucket exists.  Previous run? " + e.ToString());
                throw;
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync(bucketName).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task CreateAndDropMemcached()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucketName = nameof(BucketManagerTests) + "_" + nameof(CreateAndDropMemcached);

            try
            {
                await cluster.Buckets.CreateBucketAsync(new BucketSettings
                {
                    BucketType = BucketType.Memcached,
                    Name = bucketName,
                    RamQuotaMB = 100
                }).ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);
            }
            catch (BucketExistsException e)
            {
                _outputHelper.WriteLine("Bucket exists.  Previous run? " + e.ToString());
                throw;
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync(bucketName).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task CreateAndDropEphemeral()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucketName = nameof(BucketManagerTests) + "_" + nameof(CreateAndDropEphemeral);

            try
            {
                await cluster.Buckets.CreateBucketAsync(new BucketSettings
                {
                    BucketType = BucketType.Ephemeral,
                    Name = bucketName,
                    RamQuotaMB = 100
                }).ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);
            }
            catch (BucketExistsException e)
            {
                _outputHelper.WriteLine("Bucket exists.  Previous run? " + e.ToString());
                throw;
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync(bucketName).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task GetAllBucketsWithMemcachedBucket()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucketName = nameof(BucketManagerTests) + "_" + nameof(GetAllBucketsWithMemcachedBucket);

            try
            {
                await cluster.Buckets.CreateBucketAsync(new BucketSettings
                {
                    BucketType = BucketType.Memcached,
                    Name = bucketName,
                    RamQuotaMB = 100
                }).ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);

                var buckets = await cluster.Buckets.GetAllBucketsAsync().ConfigureAwait(false);

                Assert.Contains(buckets, x => x.Value.Name == bucketName && x.Value.BucketType == BucketType.Memcached);
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync(bucketName).ConfigureAwait(false);
            }
        }

        [Theory]
        [InlineData(BucketType.Couchbase)]
        [InlineData(BucketType.Memcached)]
        public async Task FlushBuckets(BucketType bucketType)
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucketName = nameof(BucketManagerTests) + "_" + nameof(FlushBuckets);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = bucketType,
                Name = bucketName,
                RamQuotaMB = 100,
                FlushEnabled = true
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            try
            {
                await cluster.Buckets.FlushBucketAsync(bucketName).ConfigureAwait(false);
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync(bucketName).ConfigureAwait(false);
            }
        }


        [Theory]
        [InlineData(BucketType.Couchbase)]
        [InlineData(BucketType.Memcached)]
        public async Task PingBucket(BucketType bucketType)
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucketName = nameof(BucketManagerTests) + "_" + nameof(PingBucket);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = bucketType,
                Name = bucketName,
                RamQuotaMB = 100,
                FlushEnabled = true
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            try
            {
                var bucket = await cluster.BucketAsync(bucketName).ConfigureAwait(false);

                var pingReport = await bucket.PingAsync().ConfigureAwait(false);

                Assert.Contains(pingReport.Services.Keys, x => x == "kv");
                Assert.True(pingReport.Services["kv"].All(x => x.State == Couchbase.Diagnostics.ServiceState.Ok));
            }
            finally
            {
                await _fixture.InitializeAsync().ConfigureAwait(false);
                cluster = await _fixture.GetCluster().ConfigureAwait(false);

                await cluster.Buckets.DropBucketAsync(bucketName).ConfigureAwait(false);
            }
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
                EvictionPolicy = EvictionPolicyType.FullEviction
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromMilliseconds(5000)).ConfigureAwait(false);

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
            Assert.Equal(expected.RamQuotaMB, actual.RamQuotaMB);
            Assert.Equal(expected.MaxTtl, actual.MaxTtl);
            Assert.Equal(expected.ReplicaIndexes, actual.ReplicaIndexes);
            Assert.Equal(expected.NumReplicas, actual.NumReplicas);
            Assert.Equal(expected.FlushEnabled, actual.FlushEnabled);
            Assert.Equal(expected.CompressionMode, actual.CompressionMode);
            Assert.Equal(expected.ConflictResolutionType, actual.ConflictResolutionType);
            Assert.Equal(expected.EvictionPolicy, actual.EvictionPolicy);
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

                await Task.Delay(5000).ConfigureAwait(false);

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

                await Task.Delay(5000).ConfigureAwait(false);

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

                await Task.Delay(5000).ConfigureAwait(false);

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

                await Task.Delay(5000).ConfigureAwait(false);

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

                await Task.Delay(5000).ConfigureAwait(false);

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

                await Task.Delay(5000).ConfigureAwait(false);

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

        [CouchbaseVersionDependentFact(MinVersion = "7.1.0")]
        public async Task CreateCouchbaseBucketWith_StorageBackend_Couchstore()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 100,
                StorageBackend = StorageBackend.Couchstore
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(StorageBackend.Couchstore, result.StorageBackend);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.1.0")]
        public async Task CreateCouchbaseBucketWith_StorageBackend_Magma()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 256,
                StorageBackend = StorageBackend.Magma
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(StorageBackend.Magma, result.StorageBackend);
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }
        [CouchbaseVersionDependentFact(MinVersion = "7.1.0")]
        public async Task CreateCouchbaseBucketWith_CustomConflictResolution()
        {
            var bucketManager = _fixture.Cluster.Buckets;
            var name = Guid.NewGuid().ToString();

            var settings = new BucketSettings
            {
                Name = name,
                BucketType = BucketType.Couchbase,
                RamQuotaMB = 100,
                ConflictResolutionType = ConflictResolutionType.Custom
            };

            try
            {
                // create
                await bucketManager.CreateBucketAsync(settings).ConfigureAwait(false);

                await Task.Delay(5000).ConfigureAwait(false);

                // get
                var result = await bucketManager.GetBucketAsync(settings.Name).ConfigureAwait(false);
                Assert.Equal(ConflictResolutionType.Custom, result.ConflictResolutionType);
            }
            catch (CouchbaseException ex)
            {
                Assert.True(ex.Context?.Message.Contains("Conflict resolution type 'custom' is supported only with developer preview enabled"));
            }
            finally
            {
                // drop
                await bucketManager.DropBucketAsync(settings.Name).ConfigureAwait(false);
            }
        }
    }
}
