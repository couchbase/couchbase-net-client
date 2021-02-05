using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Management
{
    [Collection("NonParallel")]
    public class QueryIndexManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public QueryIndexManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task CreateAndDropIndex()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var indexName = "indexmgr_test";
            try
            {
                await cluster.QueryIndexes.CreateIndexAsync(
                    "default", indexName, new[] { "type" }).ConfigureAwait(false);
            }
            catch (IndexExistsException)
            {
                _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            bool failedCleanup = false;
            try
            {
                await cluster.QueryIndexes.BuildDeferredIndexesAsync("default").ConfigureAwait(false);

                using var cts = new CancellationTokenSource(10000);

                await cluster.QueryIndexes.WatchIndexesAsync("default", new[] {"indexmgr_test"},
                    options => { options.CancellationToken(cts.Token); }).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    await cluster.QueryIndexes.DropIndexAsync("default", "indexmgr_test").ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Failure during cleanup: {e}");
                    failedCleanup = true;
                }
            }

            Assert.False(failedCleanup);
        }
    }
}
