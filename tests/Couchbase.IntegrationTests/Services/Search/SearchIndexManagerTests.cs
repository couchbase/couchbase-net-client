using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management;
using Couchbase.Management.Search;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Search
{
    public class SearchIndexManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public SearchIndexManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task TestSearchManager()
        {
            var cluster = _fixture.Cluster;
            var manager = cluster.SearchIndexes;

            var definition = new SearchIndex
            {
                Name = "test_index",
                Type = "fulltext-index",
                SourceName = "default",
                SourceType = "couchbase",
            };

            try
            {
                // upsert
                await manager.UpsertIndexAsync(definition).ConfigureAwait(false);

                // get
                var getResult = await manager.GetIndexAsync(definition.Name).ConfigureAwait(false);
                VerifyIndex(definition, getResult);

                //TODO: assert params, planParams and sourceParams

                // get all
                var getAllResult = await manager.GetAllIndexesAsync().ConfigureAwait(false);
                getResult = getAllResult.Single(x => x.Name == definition.Name);
                VerifyIndex(definition, getResult);

                await manager.GetIndexedDocumentsCountAsync(definition.Name).ConfigureAwait(false);

                // pause
                await manager.PauseIngestAsync(definition.Name).ConfigureAwait(false);

                // resume
                await manager.ResumeIngestAsync(definition.Name).ConfigureAwait(false);

                // freeze
                await manager.FreezePlanAsync(definition.Name).ConfigureAwait(false);

                // unfreeze
                await manager.UnfreezePlanAsync(definition.Name).ConfigureAwait(false);

                // allow querying
                await manager.AllowQueryingAsync(definition.Name).ConfigureAwait(false);

                // disallow querying
                await manager.DisallowQueryingAsync(definition.Name).ConfigureAwait(false);
            }
            finally
            {
                await manager.DropIndexAsync(definition.Name).ConfigureAwait(false);
            }
        }

        private static void VerifyIndex(SearchIndex definition, SearchIndex index)
        {
            Assert.NotNull(index);
            Assert.Equal(definition.Name, index.Name);
            Assert.Equal(definition.Type, index.Type);
            Assert.NotEmpty(index.Uuid);
            Assert.Equal(definition.SourceName, index.SourceName);
            Assert.Equal(definition.SourceType, index.SourceType);
        }
    }
}
