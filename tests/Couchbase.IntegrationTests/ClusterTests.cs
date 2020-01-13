using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Query.Couchbase.N1QL;
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
        public async Task Test_Query()
        {
            var cluster = await _fixture.GetCluster();;
            var result = await cluster.QueryAsync<dynamic>("SELECT x.* FROM `default` WHERE x.Type=0",
                options =>
                {
                     options.Encoding(Encoding.Utf8);
                     options.AddPositionalParameter("poo");
                });
        }

        [Fact]
        public async Task Test_Query2()
        {
            var cluster = await _fixture.GetCluster();

            var result = await cluster.QueryAsync<dynamic>("SELECT * FROM `default` WHERE type=$name;",
                options =>
            {
                options.AddNamedParameter("name", "person");
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
