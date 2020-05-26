using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Buckets;
using Xunit;

namespace Couchbase.IntegrationTests.Management
{
    public class BucketManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public BucketManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CreateAndDropCouchbaseBucket()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = BucketType.Couchbase,
                Name = "bucketmgr_test",
                NumReplicas = 0,
                RamQuotaMB = 100
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            await cluster.Buckets.DropBucketAsync("bucketmgr_test").ConfigureAwait(false);
        }

        [Fact]
        public async Task CreateAndDropMemcached()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = BucketType.Memcached,
                Name = "bucketmgr_test",
                RamQuotaMB = 100
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            await cluster.Buckets.DropBucketAsync("bucketmgr_test").ConfigureAwait(false);
        }

        [Fact]
        public async Task CreateAndDropEphemeral()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = BucketType.Ephemeral,
                Name = "bucketmgr_test",
                RamQuotaMB = 100
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            await cluster.Buckets.DropBucketAsync("bucketmgr_test").ConfigureAwait(false);
        }

        [Fact]
        public async Task GetAllBucketsWithMemcachedBucket()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = BucketType.Memcached,
                Name = "bucketmgr_test",
                RamQuotaMB = 100
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            try
            {
                var buckets = await cluster.Buckets.GetAllBucketsAsync().ConfigureAwait(false);

                Assert.Contains(buckets, x => x.Value.Name == "bucketmgr_test" && x.Value.BucketType == BucketType.Memcached);
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync("bucketmgr_test").ConfigureAwait(false);
            }
        }

        [Theory]
        [InlineData(BucketType.Couchbase)]
        [InlineData(BucketType.Memcached)]
        public async Task FlushBuckets(BucketType bucketType)
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = bucketType,
                Name = "bucketmgr_test",
                RamQuotaMB = 100,
                FlushEnabled = true
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            try
            {
                await cluster.Buckets.FlushBucketAsync("bucketmgr_test").ConfigureAwait(false);
            }
            finally
            {
                await cluster.Buckets.DropBucketAsync("bucketmgr_test").ConfigureAwait(false);
            }
        }


        [Theory]
        [InlineData(BucketType.Couchbase)]
        [InlineData(BucketType.Memcached)]
        public async Task PingBucket(BucketType bucketType)
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Buckets.CreateBucketAsync(new BucketSettings
            {
                BucketType = bucketType,
                Name = "bucketmgr_test",
                RamQuotaMB = 100,
                FlushEnabled = true
            }).ConfigureAwait(false);

            await Task.Delay(5000).ConfigureAwait(false);

            try
            {
                var bucket = await cluster.BucketAsync("bucketmgr_test").ConfigureAwait(false);

                var pingReport = await bucket.PingAsync().ConfigureAwait(false);

                Assert.Contains(pingReport.Services.Keys, x => x == "kv");
                Assert.True(pingReport.Services["kv"].All(x => x.State == Couchbase.Diagnostics.ServiceState.Ok));
            }
            finally
            {
                await _fixture.InitializeAsync().ConfigureAwait(false);
                cluster = await _fixture.GetCluster().ConfigureAwait(false);

                await cluster.Buckets.DropBucketAsync("bucketmgr_test").ConfigureAwait(false);
            }
        }
    }
}
