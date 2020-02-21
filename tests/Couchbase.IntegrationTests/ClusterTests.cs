using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class ClusterTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public ClusterTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_Open_More_Than_One_Bucket()
        {
            var cluster = await _fixture.GetCluster();

            var bucket1 = await cluster.BucketAsync("travel-sample");
            Assert.NotNull(bucket1);

            var bucket2 = await cluster.BucketAsync("default");
            Assert.NotNull(bucket2);
        }

        [Fact]
        public async Task Test_Query_With_Positional_Parameters()
        {
            var cluster = await _fixture.GetCluster();;
            var result = await cluster.QueryAsync<dynamic>("SELECT x.* FROM `default` WHERE x.Type=$1",
                options =>
                {
                    options.Parameter("foo");
                });

            await foreach (var row in result)
            {
            }
            result.Dispose();
        }

        [Fact]
        public async Task Test_Query2()
        {
            var cluster = await _fixture.GetCluster();

            var result = await cluster.QueryAsync<dynamic>("SELECT * FROM `default` WHERE type=$name;",
                options =>
            {
                options.Parameter("name", "person");
            }).ConfigureAwait(false);

            await foreach (var o in result)
            {
            }
            result.Dispose();
        }

        [Fact]
        public async Task Test_Views()
        {
            var cluster = _fixture.Cluster;
            var bucket = await cluster.BucketAsync("beer-sample");

            var results = await bucket.ViewQueryAsync<object, object>("beer", "brewery_beers").ConfigureAwait(false);
            await foreach (var result in results)
            {
            }
        }

        [Fact]
        public async Task Test_WaitUntilReadyAsync()
        {
            var cluster = _fixture.Cluster;
            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }
    }
}
