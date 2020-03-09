using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.Management
{
    public class AnalyticsIndexManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public AnalyticsIndexManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Cluster_AnalyticsIndexes_Not_Null()
        {
            //arrange
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            //act
            var manager = cluster.AnalyticsIndexes;

            //assert
            Assert.NotNull(manager);
        }
    }
}
