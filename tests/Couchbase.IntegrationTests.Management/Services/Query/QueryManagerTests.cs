using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Query;
using Couchbase.Test.Common;
using Xunit;

namespace Couchbase.IntegrationTests.Management.Services.Query
{
    [Collection(NonParallelDefinition.Name)]
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
                try
                {
                    // create primary
                    await queryManager.CreatePrimaryIndexAsync("default");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                catch (CouchbaseException)
                {
                    //swallow because primary index likely already exists. Follow up commit will add detail to exception thus can filter by that.
                }

                // create deferred custom index
                await queryManager.CreateIndexAsync("default", "custom", new [] { "test" }, options => options.Deferred(true));

                await Task.Delay(TimeSpan.FromSeconds(5));

                // get all
                var queryIndexes = await queryManager.GetAllIndexesAsync("default");
                Assert.True(queryIndexes.Any());

                var primaryIndex = queryIndexes.Single(index => index.Name == "#primary");
                Assert.True(primaryIndex.IsPrimary);
                Assert.Equal("online", primaryIndex.State);

                var customIndex = queryIndexes.Single(index => index.Name == "custom");
                Assert.False(customIndex.IsPrimary);
                Assert.Equal("deferred", customIndex.State);

                // build deferred
                await queryManager.BuildDeferredIndexesAsync("default");

                // watch deferred index
                await queryManager.WatchIndexesAsync("default", new[] {"custom"});
            }
            finally
            {
                // drop primary
                await queryManager.DropPrimaryIndexAsync("default");

                // drop custom
                await queryManager.DropIndexAsync("default", "custom");
            }
        }
    }
}
