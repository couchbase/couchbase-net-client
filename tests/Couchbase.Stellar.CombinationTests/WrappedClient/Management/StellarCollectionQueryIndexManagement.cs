using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Collections;
using Couchbase.Management.Query;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient.Management;

[Collection(StellarTestCollection.Name)]
public class StellarCollectionQueryIndexManagement
{
    private readonly ITestOutputHelper _outputHelper;
    private StellarFixture _fixture;
    private ConsistencyUtils _utils;
    public StellarCollectionQueryIndexManagement(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
        _utils = new ConsistencyUtils(_fixture);
    }

    [Fact]
    public async Task CreateAndDropCollectionIndex()
    {
        var bucket = await _fixture.DefaultBucket().ConfigureAwait(false);
        var collectionManager = _fixture.DefaultBucket().Result.Collections;

        var scopeName = Guid.NewGuid().ToString();
        var collectionName = Guid.NewGuid().ToString();
        var collectionSpec = new CollectionSpec(scopeName, collectionName);

        try
        {
            await collectionManager.CreateScopeAsync(scopeName);
            await _utils.WaitUntilScopeIsPresent(scopeName).ConfigureAwait(false);
            await collectionManager.CreateCollectionAsync(collectionSpec);
            await _utils.WaitUntilCollectionIsPresent(collectionName, scopeName: scopeName).ConfigureAwait(false);

            var scope = await bucket.ScopeAsync(scopeName).ConfigureAwait(false);
            var collection = await scope.CollectionAsync(collectionName).ConfigureAwait(false);

            const string indexName = "indexmgr_test_collection";
            try
            {
                await collection.QueryIndexes.CreateIndexAsync(indexName, new[] { "type" }, new CreateQueryIndexOptions())
                    .ConfigureAwait(false);
            }
            catch (IndexExistsException)
            {
                _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            var failedCleanup = false;
            try
            {
                await collection.QueryIndexes.BuildDeferredIndexesAsync(new BuildDeferredQueryIndexOptions())
                    .ConfigureAwait(false);

                using var cts = new CancellationTokenSource(10000);

                var getIndexes = await collection.QueryIndexes.GetAllIndexesAsync(new GetAllQueryIndexOptions())
                    .ConfigureAwait(false);
                Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
            }
            finally
            {
                try
                {
                    await collection.QueryIndexes.DropIndexAsync(indexName, new DropQueryIndexOptions())
                        .ConfigureAwait(false);
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
