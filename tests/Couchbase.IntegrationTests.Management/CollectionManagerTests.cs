
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management;
using Couchbase.Management.Collections;
using Xunit;

namespace Couchbase.IntegrationTests
{
    [Collection("NonParallel")]
    public class CollectionManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public CollectionManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [CouchbaseVersionDependentFact(MinVersion = "6.5.1")]
        public async Task Test_CollectionManager()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope", collectionName = "test_collection";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName).ConfigureAwait(false);
                Assert.True(scopeExistsResult);

                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName).ConfigureAwait(false);
                Assert.Equal(scopeName, getScopeResult.Name);

                // get all scopes
                var getAllScopesResult = await collectionManager.GetAllScopesAsync().ConfigureAwait(false);
                var scope = getAllScopesResult.SingleOrDefault(x => x.Name == scopeName);
                Assert.NotNull(scope);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                Assert.True(collectionExistsResult);
            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_ScopeNotFound()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope1", collectionName = "test_collection1";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);
            var collectionSpecInvalid = new CollectionSpec("noscope", "emptycollection");

            try
            {
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);


                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName).ConfigureAwait(false);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpecInvalid).ConfigureAwait(false);

                var scopeExistsResult = await collectionManager.ScopeExistsAsync("noscope").ConfigureAwait(false);
                Assert.False(scopeExistsResult);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpecInvalid).ConfigureAwait(false);
                Assert.False(collectionExistsResult);


            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }

        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_ScopeExists()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope2", collectionName = "test_collection2";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName).ConfigureAwait(false);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);
                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                Assert.True(collectionExistsResult);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName).ConfigureAwait(false);
                Assert.True(scopeExistsResult);


            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }

        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_CollectionExists()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope3", collectionName = "test_collection3";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                Assert.True(collectionExistsResult);

            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_ScopeExistsException()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope4", collectionName = "test_collection4";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName).ConfigureAwait(false);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                Assert.True(collectionExistsResult);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName).ConfigureAwait(false);
                Assert.True(scopeExistsResult);

                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

            }
            catch (ScopeExistsException e)
            {
                Assert.Equal("Scope with name test_scope4 already exists", e.Message);
            }

            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }

        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_CollectionExistsException()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope5", collectionName = "test_collection5";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName).ConfigureAwait(false);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                Assert.True(collectionExistsResult);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName).ConfigureAwait(false);
                Assert.True(scopeExistsResult);

                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);


            }
            catch (CollectionExistsException e)
            {
                Assert.Equal("Collection with name test_collection5 already exists in scope test_scope5", e.Message);
            }

            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }

        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_DropNonExistentScope()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope6", collectionName = "test_collection6";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);


            try
            {
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName).ConfigureAwait(false);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

                await collectionManager.DropScopeAsync("scope_none").ConfigureAwait(false);

            }
            catch (ScopeNotFoundException e)
            {
                Assert.Equal("Scope with name scope_none not found", e.Message);
            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }

        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_DropNonExistentCollection()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope6", collectionName = "test_collection6";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            var collectionSpecNone = new CollectionSpec("scope_only", "collection_null");
            var scopeSpec1 = new ScopeSpec("scope_only");

            try
            {
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

                // get scope
                var getScopeResult = await collectionManager.GetScopeAsync(scopeName).ConfigureAwait(false);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

                await collectionManager.CreateScopeAsync(scopeSpec1).ConfigureAwait(false);

                await collectionManager.DropCollectionAsync(collectionSpecNone).ConfigureAwait(false);


            }
            catch (CollectionNotFoundException e)
            {
                Assert.Equal("Collection with name collection_null not found in scope scope_only", e.Message);
            }
            finally
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec).ConfigureAwait(false);

                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }

        }


        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_BatchingCollectionGet()
        {
            var tasks = new List<Task>();
            var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);

            for (var i = 0; i < 20; i++)
            {
                var task = collection.GetAsync($"mykey-{i}");
                tasks.Add(task);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_UpsertOps()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "my_scope1", collectionName = "my_collection1";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            // create scope
            await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

            // create collection
            await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

            var collection = bucket.Scope(scopeName).Collection(collectionName);
            var tasks = new List<Task>();

            for (var i = 0; i < 20; i++)
            {
                var task = await collection.UpsertAsync($"mykey-{i}", new { });

            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_InsertOps()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "my_scope1", collectionName = "my_collection1";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            // create scope
            await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

            // create collection
            await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

            var collection = bucket.Scope(scopeName).Collection(collectionName);
            var tasks = new List<Task>();

            for (var i = 0; i < 20; i++)
            {
                var task = await collection.InsertAsync($"mykey-{i}", new { }).ConfigureAwait(false);

            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_RemoveOps()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "my_scope1", collectionName = "my_collection1";
            var scopeSpec = new ScopeSpec(scopeName);
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            // create scope
            await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);

            // create collection
            await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

            var collection = bucket.Scope(scopeName).Collection(collectionName);
            var tasks = new List<Task>();

            for (var i = 0; i < 20; i++)
            {
                await collection.RemoveAsync($"mykey-{i}").ConfigureAwait(false);

            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_GetAllScopes()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName1 = "test_scopex1", collectionName = "test_collection1", scopeName2 = "test_scopex2", scopeName3 = "test_scopex3", scopeName4 = "test_scopex4";
            var scopeSpec1 = new ScopeSpec(scopeName1);
            var scopeSpec2 = new ScopeSpec(scopeName2);
            var scopeSpec3 = new ScopeSpec(scopeName3);
            var scopeSpec4 = new ScopeSpec(scopeName4);
            var collectionSpec = new CollectionSpec(scopeName1, collectionName);
            // create scope
            await collectionManager.CreateScopeAsync(scopeSpec1).ConfigureAwait(false);
            await collectionManager.CreateScopeAsync(scopeSpec2).ConfigureAwait(false);
            await collectionManager.CreateScopeAsync(scopeSpec3).ConfigureAwait(false);
            await collectionManager.CreateScopeAsync(scopeSpec4).ConfigureAwait(false);

            // get all scopes
            var getAllScopesResult = await collectionManager.GetAllScopesAsync().ConfigureAwait(false);
            Assert.NotNull(getAllScopesResult);
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_SingleScopeMaxNumberOfCollections()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            var collectionManager = (CollectionManager)bucket.Collections;
            string scopeName = "singlescope1";
            var scopeSpec = new ScopeSpec(scopeName);
            try
            {
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);
                for (int i = 0; i < 1000; i++)
                {
                    var collectionSpec = new CollectionSpec(scopeName, (1000 + i).ToString());
                    await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);
                    var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                    Assert.True(collectionExistsResult);
                }
            }
            finally
            {
                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }
        }
    }
}
