using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Collections;
using Couchbase.Management.Query;
using Couchbase.Stellar.CombinationTests.Fixtures;
using Couchbase.Stellar.CombinationTests.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.Management;

[Collection(StellarTestCollection.Name)]
public class StellarCollectionQueryIndexManagementTests
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly StellarFixture _fixture;
    private readonly ConsistencyUtils _utils;
    public StellarCollectionQueryIndexManagementTests(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
        _utils = new ConsistencyUtils(_fixture);
    }

    [Fact]
    public async Task CreateAndDropCollectionIndex()
    {
        var bucket = await _fixture.DefaultBucket();
        var collectionManager = bucket.Collections;

        var scopeName = Guid.NewGuid().ToString();
        var collectionName = Guid.NewGuid().ToString();

        try
        {
            await collectionManager.CreateScopeAsync(scopeName);
            await _utils.WaitUntilScopeIsPresent(scopeName);
            await collectionManager.CreateCollectionAsync(scopeName, collectionName, new CreateCollectionSettings());
            await _utils.WaitUntilCollectionIsPresent(collectionName, scopeName: scopeName);

            var scope = await bucket.ScopeAsync(scopeName);
            var collection = await scope.CollectionAsync(collectionName);

            const string indexName = "indexmgr_test_collection";
            try
            {
                await collection.QueryIndexes.CreateIndexAsync(indexName,
                        ["type"], new CreateQueryIndexOptions())
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
