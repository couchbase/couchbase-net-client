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
            var bucket = await cluster.BucketAsync("default");

            var results = await bucket.ViewQueryAsync("dev_test_ddoc", "test_view").ConfigureAwait(false);
            await foreach (var result in results)
            {
            }
        }
    }
}
