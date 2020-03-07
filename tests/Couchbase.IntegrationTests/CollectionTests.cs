using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class CollectionTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public CollectionTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_Collection_Exists()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "my_scope", collectionName = "my_collection";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                // create scope
                //await collectionManager.CreateScopeAsync(scopeSpec);

                // create collection
                // await collectionManager.CreateCollectionAsync(collectionSpec);

                var collection = bucket.Scope(scopeName).Collection(collectionName);
                var result = await collection.UpsertAsync("key3", new { });

                var result2 = await collection.UpsertAsync("key3", new { boo="bee"}, new UpsertOptions().Expiry(TimeSpan.FromMilliseconds(100000)));


            }
            catch
            {
                // ???
            }
            finally
            {
                // drop collection
                //await collectionManager.DropCollectionAsync(collectionSpec);
                //await collectionManager.DropScopeAsync(scopeName);
            }
        }
    }
}
