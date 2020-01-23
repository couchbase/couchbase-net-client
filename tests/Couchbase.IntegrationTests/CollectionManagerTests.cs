using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management;
using Couchbase.Management.Collections;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class CollectionManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public CollectionManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_CollectionManager()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager) bucket.Collections;

            const string scopeName = "test_scope", collectionName = "test_collection";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeSpec);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName);
                Assert.True(scopeExistsResult);

                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                // get all scopes
                var getAllScopesResult = await collectionManager.GetAllScopesAsync();
                var scope = getAllScopesResult.SingleOrDefault(x => x.Name == scopeName);
                Assert.NotNull(scope);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec);
                Assert.True(collectionExistsResult);
            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName);
            }
        }
    }
}
