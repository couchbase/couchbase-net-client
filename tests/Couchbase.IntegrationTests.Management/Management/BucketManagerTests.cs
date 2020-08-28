using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Buckets;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Management
{
    [Collection("NonParallel")]
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
            var bucketName = nameof(IntegrationTests.BucketManagerTests) + "_" + nameof(CreateAndDropCouchbaseBucket);

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
            var bucketName = nameof(IntegrationTests.BucketManagerTests) + "_" + nameof(CreateAndDropMemcached);

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
            var bucketName = nameof(IntegrationTests.BucketManagerTests) + "_" + nameof(CreateAndDropEphemeral);

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
            var bucketName = nameof(IntegrationTests.BucketManagerTests) + "_" + nameof(GetAllBucketsWithMemcachedBucket);

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
            var bucketName = nameof(IntegrationTests.BucketManagerTests) + "_" + nameof(FlushBuckets);

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
            var bucketName = nameof(IntegrationTests.BucketManagerTests) + "_" + nameof(PingBucket);

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
    }
}
