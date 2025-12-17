using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management;
using Couchbase.Management.Search;
using Couchbase.Test.Common;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Search
{
    [Collection(NonParallelDefinition.Name)]
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
                await manager.UpsertIndexAsync(definition).ConfigureAwait(true);

                // get
                var getResult = await manager.GetIndexAsync(definition.Name).ConfigureAwait(true);
                VerifyIndex(definition, getResult);

                //TODO: assert params, planParams and sourceParams

                // get all
                var getAllResult = await manager.GetAllIndexesAsync().ConfigureAwait(true);
                getResult = getAllResult.Single(x => x.Name == definition.Name);
                VerifyIndex(definition, getResult);

                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);

                await manager.GetIndexedDocumentsCountAsync(definition.Name).ConfigureAwait(true);

                // pause
                await manager.PauseIngestAsync(definition.Name).ConfigureAwait(true);

                // resume
                await manager.ResumeIngestAsync(definition.Name).ConfigureAwait(true);

                // freeze
                await manager.FreezePlanAsync(definition.Name).ConfigureAwait(true);

                // unfreeze
                await manager.UnfreezePlanAsync(definition.Name).ConfigureAwait(true);

                // allow querying
                await manager.AllowQueryingAsync(definition.Name).ConfigureAwait(true);

                // disallow querying
                await manager.DisallowQueryingAsync(definition.Name).ConfigureAwait(true);
            }
            finally
            {
                await manager.DropIndexAsync(definition.Name).ConfigureAwait(true);
            }
        }

        private static void VerifyIndex(SearchIndex definition, SearchIndex index)
        {
            Assert.NotNull(index);
            Assert.Equal(definition.Name, index.Name);
            Assert.Equal(definition.Type, index.Type);
            Assert.NotEmpty(index.Uuid);
        }
    }
}
