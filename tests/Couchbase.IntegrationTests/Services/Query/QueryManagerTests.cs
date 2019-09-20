using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Query;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Query
{
    public class QueryManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public QueryManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_QueryManager()
        {
            var cluster = _fixture.Cluster;
            var queryManager = cluster.QueryIndexes;

            try
            {
                // create primary
                await queryManager.CreatePrimaryAsync("default");

                // create deferred custom index
                await queryManager.CreateAsync("default", "custom", new [] { "test" }, options => options.WithDeferred(true));

                // get all
                var queryIndexes = await queryManager.GetAllAsync("default");
                Assert.True(queryIndexes.Any());

                var primaryIndex = queryIndexes.Single(index => index.Name == "#primary");
                Assert.True(primaryIndex.IsPrimary);
                Assert.Equal("online", primaryIndex.State);

                var customIndex = queryIndexes.Single(index => index.Name == "custom");
                Assert.False(customIndex.IsPrimary);
                Assert.Equal("deferred", customIndex.State);

                // build deferred
                await queryManager.BuildDeferredAsync("default");

                // watch deferred index
                await queryManager.WatchAsync("default", new[] {"custom"});
            }
            finally
            {
                // drop primary
                await queryManager.DropPrimaryAsync("default");

                // drop custom
                await queryManager.DropAsync("default", "custom");
            }
        }
    }
}
