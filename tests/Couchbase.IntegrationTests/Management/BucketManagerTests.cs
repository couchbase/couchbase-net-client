using System;
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
        public async Task CreateAndDropBucket()
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
    }
}
