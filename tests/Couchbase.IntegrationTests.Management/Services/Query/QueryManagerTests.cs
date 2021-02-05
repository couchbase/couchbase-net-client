using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Query;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Query
{
    [Collection("NonParallel")]
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
                await queryManager.CreatePrimaryIndexAsync("default").ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                // create deferred custom index
                await queryManager.CreateIndexAsync("default", "custom", new [] { "test" }, options => options.Deferred(true)).ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                // get all
                var queryIndexes = await queryManager.GetAllIndexesAsync("default").ConfigureAwait(false);
                Assert.True(queryIndexes.Any());

                var primaryIndex = queryIndexes.Single(index => index.Name == "#primary");
                Assert.True(primaryIndex.IsPrimary);
                Assert.Equal("online", primaryIndex.State);

                var customIndex = queryIndexes.Single(index => index.Name == "custom");
                Assert.False(customIndex.IsPrimary);
                Assert.Equal("deferred", customIndex.State);

                // build deferred
                await queryManager.BuildDeferredIndexesAsync("default").ConfigureAwait(false);

                // watch deferred index
                await queryManager.WatchIndexesAsync("default", new[] {"custom"}).ConfigureAwait(false);
            }
            finally
            {
                // drop primary
                await queryManager.DropPrimaryIndexAsync("default").ConfigureAwait(false);

                // drop custom
                await queryManager.DropIndexAsync("default", "custom").ConfigureAwait(false);
            }
        }
    }
}
