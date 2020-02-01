using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Xunit;

namespace Couchbase.IntegrationTests.Management
{
    public class QueryIndexManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public QueryIndexManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CreateAndDropIndex()
        {
            var cluster = await _fixture.GetCluster();

            await cluster.QueryIndexes.CreateIndexAsync(
                "default", "indexmgr_test", new[] {"type"});

            try
            {
                await cluster.QueryIndexes.BuildDeferredIndexesAsync("default");

                using var cts = new CancellationTokenSource(10000);

                await cluster.QueryIndexes.WatchIndexesAsync("default", new[] {"indexmgr_test"}, options =>
                    {
                        options.CancellationToken(cts.Token);
                    });
            }
            finally
            {
                await cluster.QueryIndexes.DropIndexAsync("default", "indexmgr_test");
            }
        }
    }
}
