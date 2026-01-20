using System;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
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

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_Collection_Exists()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "my_scope", collectionName = "my_collection";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeName);

                //create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                await Task.Delay(1000);

                var scope = await bucket.ScopeAsync(scopeName);
                var collection = await scope.CollectionAsync(collectionName);
                var result = await collection.UpsertAsync("key3", new { });

                var result2 = await collection.UpsertAsync("key3", new { boo="bee"}, new UpsertOptions().Expiry(TimeSpan.FromMilliseconds(100000)));
            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec);
                await collectionManager.DropScopeAsync(scopeName);
            }
        }

        [Fact]
        public async Task InsertByteArray_DefaultConverter_UnsupportedException()
        {
            const string key = nameof(InsertByteArray_DefaultConverter_UnsupportedException);

            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collection = await bucket.DefaultCollectionAsync();

            try
            {
                await Assert.ThrowsAsync<UnsupportedException>(
                    () => collection.InsertAsync(key, new byte[] { 1, 2, 3 }));
            }
            finally
            {
                try
                {
                    await collection.RemoveAsync(key);
                }
                catch (DocumentNotFoundException)
                {
                }
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task CollectionIdChanged_RetriesAutomatically()
        {
            const string scopeName = "CollectionIdChanged";
            const string collectionName = "coll";
            const string key = nameof(CollectionIdChanged_RetriesAutomatically);

            var bucket = await _fixture.GetDefaultBucket();
            var collectionManager = bucket.Collections;

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(new CollectionSpec(scopeName, collectionName));
                await Task.Delay(1000);

                var scope = await bucket.ScopeAsync(scopeName);
                ICouchbaseCollection collection = await scope.CollectionAsync(collectionName);

                await collection.UpsertAsync(key, new {name = "mike"});

                await collectionManager.DropCollectionAsync(new CollectionSpec(scopeName, collectionName));
                await Task.Delay(500);

                await collectionManager.CreateCollectionAsync(new CollectionSpec(scopeName, collectionName));
                await Task.Delay(500);

                await collection.UpsertAsync(key, new {name = "mike"});
            }
            finally
            {
                await collectionManager.DropScopeAsync(scopeName);
            }
        }
    }
}
