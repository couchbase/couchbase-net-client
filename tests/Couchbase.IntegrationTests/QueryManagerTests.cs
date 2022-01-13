using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Query;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests
{
    public class QueryManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;
        public QueryManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Test_IgnoreIfExists_True()
        {
            var manager = _fixture.Cluster.QueryIndexes;
            await manager.CreatePrimaryIndexAsync("default",
                new CreatePrimaryQueryIndexOptions()
                    .Deferred(false)
                    .IgnoreIfExists(true));

            await manager.CreatePrimaryIndexAsync("default",
                new CreatePrimaryQueryIndexOptions()
                    .Deferred(false)
                    .IgnoreIfExists(true));
        }

        [Fact]
        public async Task Test_Create_Existing_Index_Throws_IndexExistsException()
        {
            var idxmgr = _fixture.Cluster.QueryIndexes;

            await idxmgr.CreatePrimaryIndexAsync("default",
               new CreatePrimaryQueryIndexOptions()
                   .Deferred(false)
                   .IgnoreIfExists(true));

            await Assert.ThrowsAsync<IndexExistsException>(async () => await idxmgr.CreatePrimaryIndexAsync("default"));
        }

        [Fact]
        public async Task CreatePrimaryIndex()
        {
            await _fixture.Cluster.QueryIndexes.CreatePrimaryIndexAsync("`default`",
                options =>
                {
                    options.IndexName("named_primary_index");
                    options.IgnoreIfExistsValue = true;
                });

            await Task.Delay(1000);

            await Assert.ThrowsAsync<IndexExistsException>(async () =>
            {
                await _fixture.Cluster.QueryIndexes.CreatePrimaryIndexAsync("`default`",
                options =>
                {
                    options.IndexName("named_primary_index");
                    options.IgnoreIfExistsValue = false;
                });
            });
        }
    }
}
