using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Query;
using Couchbase.Test.Common;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Management
{
    [Collection(NonParallelDefinition.Name)]
    public class CollectionQueryIndexManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public CollectionQueryIndexManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task CreateAndDropCollectionIndex()
        {
            var bucket = await _fixture.GetDefaultBucket();
            var collectionManager = _fixture.GetDefaultBucket().Result.Collections;

            var scopeName = Guid.NewGuid().ToString();
            var collectionName = Guid.NewGuid().ToString();
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(collectionSpec);

                var scope = await bucket.ScopeAsync(scopeName);
                var collection = await scope.CollectionAsync(collectionName);

                const string indexName = "indexmgr_test_collection";
                try
                {
                    await collection.QueryIndexes.CreateIndexAsync(indexName, new[] { "type" }, new CreateQueryIndexOptions())
                        ;
                }
                catch (IndexExistsException)
                {
                    _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
                }

                var failedCleanup = false;
                try
                {
                    await collection.QueryIndexes.BuildDeferredIndexesAsync(new BuildDeferredQueryIndexOptions())
                        ;

                    using var cts = new CancellationTokenSource(10000);

                    await collection.QueryIndexes.WatchIndexesAsync(new[] { indexName }, TimeSpan.FromMinutes(1),
                            new WatchQueryIndexOptions().CancellationToken(cts.Token))
                        ;

                    var getIndexes = await collection.QueryIndexes.GetAllIndexesAsync(new GetAllQueryIndexOptions())
                        ;
                    Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
                }
                finally
                {
                    try
                    {
                        await collection.QueryIndexes.DropIndexAsync(indexName, new DropQueryIndexOptions())
                            ;
                    }
                    catch (Exception e)
                    {
                        _outputHelper.WriteLine($"Failure during cleanup: {e}");
                        failedCleanup = true;
                    }
                }

                Assert.False(failedCleanup);
            }
            finally
            {
                await collectionManager.DropScopeAsync(scopeName);
            }
        }
    }
}
