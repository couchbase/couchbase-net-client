using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management.Collections;
using Couchbase.Query;
using Couchbase.Test.Common;
using Couchbase.Test.Common.Fixtures;
using Couchbase.Test.Common.Utils;
using Xunit;
using Xunit.Abstractions;
using CollectionNotFoundException = Couchbase.Management.Collections.CollectionNotFoundException;

namespace Couchbase.IntegrationTests.Management
{
    [Collection(NonParallelDefinition.Name)]
    public class CollectionManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public CollectionManagerTests(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _outputHelper = testOutputHelper;
        }

        #region Helper methods

        private async Task DropScopeIfExists(string scopeName, CollectionManager collectionManager)
        {
            if (await collectionManager.ScopeExistsAsync(scopeName))
            {
                // drop scope
                await collectionManager.DropScopeAsync(scopeName);
            }
        }

        private async Task DropCollectionIfExists(CollectionSpec collectionSpec, CollectionManager collectionManager)
        {
            if (await collectionManager.CollectionExistsAsync(collectionSpec))
            {
                // drop collection
                await collectionManager.DropCollectionAsync(collectionSpec.ScopeName, collectionSpec.Name);
            }
        }

        private async Task DropScopeAndCollectionIfExists(string scopeName, CollectionSpec collectionSpec,
            CollectionManager collectionManager)
        {
            await DropCollectionIfExists(collectionSpec, collectionManager);
            await DropScopeIfExists(scopeName, collectionManager);
        }

        #endregion

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_CollectionManager_With_MinExpiry()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope1", collectionName = "test_collection1";
            var collectionSpec = new CollectionSpec(scopeName, collectionName)
            {
                MaxExpiry = TimeSpan.FromMinutes(10)
            };

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeName);

                await Task.Delay(TimeSpan.FromSeconds(5));

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName);
                Assert.True(scopeExistsResult);

                // get all scopes
                var getAllScopesResult = await collectionManager.GetAllScopesAsync();
                var scope = getAllScopesResult.SingleOrDefault(x => x.Name == scopeName);
                Assert.NotNull(scope);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                await Task.Delay(TimeSpan.FromSeconds(5));

                // collection exists
                getAllScopesResult = await collectionManager.GetAllScopesAsync();
                scope = getAllScopesResult.SingleOrDefault(x => x.Name == scopeName);

                Assert.Equal(TimeSpan.FromMinutes(10), scope.Collections.First(x=>x.Name== collectionName).MaxExpiry);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_CollectionManager()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope2", collectionName = "test_collection2";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeName);

                await Task.Delay(TimeSpan.FromSeconds(1));

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName);
                Assert.True(scopeExistsResult);

                // get scope
                var getScopeResult = await collectionManager.GetScopeUsingGetAllScopesAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                // get all scopes
                var getAllScopesResult = await collectionManager.GetAllScopesAsync();
                var scope = getAllScopesResult.SingleOrDefault(x => x.Name == scopeName);
                Assert.NotNull(scope);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                await Task.Delay(TimeSpan.FromSeconds(1));

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec);
                Assert.True(collectionExistsResult);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_ScopeNotFound()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope3", collectionName = "test_collection3";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);
            var collectionSpecInvalid = new CollectionSpec("noscope", "emptycollection");

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);

                // get scope
                var getScopeResult = await collectionManager.GetScopeUsingGetAllScopesAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await Assert.ThrowsAsync<ScopeNotFoundException>(async ()=> await collectionManager.CreateCollectionAsync(collectionSpecInvalid));
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_ScopeExists()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope4", collectionName = "test_collection4";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);

                // get scope
                var getScopeResult = await collectionManager.GetScopeUsingGetAllScopesAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);
                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec);
                Assert.True(collectionExistsResult);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName);
                Assert.True(scopeExistsResult);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_CollectionExists()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope5", collectionName = "test_collection5";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeName);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec);
                Assert.True(collectionExistsResult);

            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_ScopeExistsException()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope6", collectionName = "test_collection6";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);

                // get scope
                var getScopeResult = await collectionManager.GetScopeUsingGetAllScopesAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                // collection exists
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec);
                Assert.True(collectionExistsResult);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName);
                Assert.True(scopeExistsResult);

                await collectionManager.CreateScopeAsync(scopeName);

            }
            catch (ScopeExistsException e)
            {
                Assert.Equal("Scope with name test_scope6 already exists", e.Message);
            }

            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_CollectionExistsException()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope7", collectionName = "test_collection7";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);

                // get scope
                var getScopeResult = await collectionManager.GetScopeUsingGetAllScopesAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                // collection exists
                var collectionExistsResult =
                    await collectionManager.CollectionExistsAsync(collectionSpec);
                Assert.True(collectionExistsResult);

                // scope exists
                var scopeExistsResult = await collectionManager.ScopeExistsAsync(scopeName);
                Assert.True(scopeExistsResult);

                await Assert
                    .ThrowsAsync<CollectionExistsException>(async () =>
                        await collectionManager.CreateCollectionAsync(collectionSpec));

            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_DropNonExistentScope_Throws_ScopeNotFoundException()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope8", collectionName = "test_collection8";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);

                // get scope
                var getScopeResult = await collectionManager.GetScopeUsingGetAllScopesAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                // create collection
                await collectionManager.CreateCollectionAsync(collectionSpec);

                await collectionManager.DropScopeAsync("scope_none");

            }
            catch (ScopeNotFoundException e)
            {
                Assert.Equal("Scope with name scope_none not found", e.Message);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_DropNonExistentCollection_Throws_CollectionNotFoundException()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "scope_only";
            var collectionSpecNone = new CollectionSpec(scopeName, "collection_null");

            await DropScopeAndCollectionIfExists(scopeName, collectionSpecNone, collectionManager);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);

                // get scope
                var getScopeResult = await collectionManager.GetScopeUsingGetAllScopesAsync(scopeName);
                Assert.Equal(scopeName, getScopeResult.Name);

                await collectionManager.DropCollectionAsync(collectionSpecNone.ScopeName, collectionSpecNone.Name);
            }
            catch (CollectionNotFoundException e)
            {
                Assert.Equal("Collection with name collection_null not found in scope scope_only", e.Message);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpecNone, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0", Skip = "Undo to run")]
        public async Task Test_BatchingCollectionGet()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var upsertTasks = Enumerable.Range(0, 20).Select(x => collection.UpsertAsync($"mykey-{x}", x));
            var getTasks = Enumerable.Range(0, 20).Select(x => collection.GetAsync($"mykey-{x}"));

            await Task.WhenAll(upsertTasks);
            await Task.WhenAll(getTasks);
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_UpsertOps()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "my_scope9", collectionName = "my_collection9";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);

            try
            {
                // create scope and collection
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(collectionSpec);

                var scope = await bucket.ScopeAsync(scopeName);
                var collection = await scope.CollectionAsync(collectionName);

                var tasks = Enumerable.Range(0, 20).Select(x => collection.UpsertAsync($"mykey-{x}", new { }));
                await Task.WhenAll(tasks);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_InsertOps()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName = "test_scope12", collectionName = "test_collection12";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);

            try
            {
                // create scope and collection
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(collectionSpec);

                var scope = await bucket.ScopeAsync(scopeName);
                var collection = await scope.CollectionAsync(collectionName);

                var tasks = Enumerable.Range(0, 20).Select(x => collection.InsertAsync($"mykey-{x}", new { }));
                await Task.WhenAll(tasks);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_RemoveOps()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager) bucket.Collections;

            const string scopeName = "test_scope13", collectionName = "test_collection13";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);

            try
            {
                // create scope and collection
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(collectionSpec);

                var scope = await bucket.ScopeAsync(scopeName);
                var collection = await scope.CollectionAsync(collectionName);

                var insertTasks = Enumerable.Range(0, 20).Select(x => collection.InsertAsync($"mykey-{x}", new { }));
                await Task.WhenAll(insertTasks);

                var removeTasks = Enumerable.Range(0, 20).Select(x => collection.RemoveAsync($"mykey-{x}"));
                await Task.WhenAll(removeTasks);
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_GetAllScopes()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;

            const string scopeName1 = "test_scopex1",  scopeName2 = "test_scopex2", scopeName3 = "test_scopex3", scopeName4 = "test_scopex4";

            await DropScopeIfExists(scopeName1, collectionManager);
            await DropScopeIfExists(scopeName2, collectionManager);
            await DropScopeIfExists(scopeName3, collectionManager);
            await DropScopeIfExists(scopeName4, collectionManager);

            try
            {
                // create scope
                await collectionManager.CreateScopeAsync(scopeName1);
                await collectionManager.CreateScopeAsync(scopeName2);
                await collectionManager.CreateScopeAsync(scopeName3);
                await collectionManager.CreateScopeAsync(scopeName4);

                // get all scopes
                var getAllScopesResult = (await collectionManager.GetAllScopesAsync()).ToList();
                Assert.Contains(getAllScopesResult, p => p.Name == scopeName1);
                Assert.Contains(getAllScopesResult, p => p.Name == scopeName2);
                Assert.Contains(getAllScopesResult, p => p.Name == scopeName3);
                Assert.Contains(getAllScopesResult, p => p.Name == scopeName4);
            }
            finally
            {
                await DropScopeIfExists(scopeName1, collectionManager);
                await DropScopeIfExists(scopeName2, collectionManager);
                await DropScopeIfExists(scopeName3, collectionManager);
                await DropScopeIfExists(scopeName4, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0", Skip = "Undo to run.")]
        public async Task Test_SingleScopeMaxNumberOfCollections()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;
            string scopeName = "singlescope1";

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);
                for (int i = 0; i < 1000; i++)
                {
                    var collectionSpec = new CollectionSpec(scopeName, (1000 + i).ToString());
                    await collectionManager.CreateCollectionAsync(collectionSpec);
                    var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec);
                    Assert.True(collectionExistsResult);
                }
            }
            finally
            {
                await DropScopeIfExists(scopeName, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async System.Threading.Tasks.Task Test_Collections_QueryOptionsAsync()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;
            var scopeName = "query_test_scope1";
            var collectionName = "query_test_collection1";
            var docId = "mydoc1";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(collectionSpec);

                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec);
                Assert.True(collectionExistsResult);

                var scope = await bucket.ScopeAsync(scopeName);
                var collection = await scope.CollectionAsync(collectionName);

                var task = await collection.InsertAsync(docId, new { });
                var options = new QueryOptions("select * from `" + collectionName + "` where meta().id=\"" + docId + "\"") { QueryContext = "namespace:bucket:scope:collection" };
                var args = options.GetFormValues();
                Assert.Equal("namespace:bucket:scope:collection", args["query_context"]);
            }
            finally
            {
                await DropScopeIfExists(scopeName, collectionManager);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async System.Threading.Tasks.Task Test_Collections_QueryOps()
        {
            var bucket = await _fixture.Cluster.BucketAsync("default");
            var collectionManager = (CollectionManager)bucket.Collections;
            var scopeName = "query_test_scope2";
            var collectionName = "query_test_collection2";
            var docId = "mydoc2";
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(collectionSpec);

                var scope = await bucket.ScopeAsync(scopeName);
                var collection = await scope.CollectionAsync(collectionName);

                var task = await collection.InsertAsync(docId, new { });
                var options =
                    new QueryOptions("select * from `" + collectionName + "` where meta().id=\"" + docId + "\"")
                        {QueryContext = "namespace:bucket:scope:collection"};
                var args = options.GetFormValues();
                Assert.Equal("namespace:bucket:scope:collection", args["query_context"]);
                options = new QueryOptions("SELECT * FROM `$bucket` WHERE collectionName=$name")
                    .Parameter("bucket", "default").Parameter("collectionName", "query_test_collection2");

                var values = options.GetFormValues();
                Assert.Equal("default", values["$bucket"]);
                Assert.Equal("query_test_collection2", values["$collectionName"]);
            }
            catch (CouchbaseException e)
            {
                _outputHelper.WriteLine(e.ToString());
            }
            finally
            {
                await DropScopeAndCollectionIfExists(scopeName, collectionSpec, collectionManager);
            }
        }
    }

    internal static class CollectionManagerTestExtensions
    {
        public static async Task<ScopeSpec> GetScopeUsingGetAllScopesAsync(this ICouchbaseCollectionManager collectionManager, string scopeName)
        {
            var getAllScopesResult = await collectionManager.GetAllScopesAsync();
            var scope = getAllScopesResult.SingleOrDefault(x => x.Name == scopeName);
            return scope;
        }
    }
}
