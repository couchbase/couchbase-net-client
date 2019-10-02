using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Query;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Query
{
    public class QueryTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public QueryTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_Query()
        {
            var cluster = _fixture.Cluster;
            await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` LIMIT 1;");
        }

        [Fact]
        public async Task Test_Prepared()
        {
            var cluster = _fixture.Cluster;

            // execute prepare first time
            var result = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` LIMIT 1;",
                options => options.AdHoc(false));
            Assert.Equal(QueryStatus.Success, result.Status);

            // should use prepared plan
            var preparedResult = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` LIMIT 1;",
                options => options.AdHoc(false));
            Assert.Equal(QueryStatus.Success, preparedResult.Status);
        }
    }
}
