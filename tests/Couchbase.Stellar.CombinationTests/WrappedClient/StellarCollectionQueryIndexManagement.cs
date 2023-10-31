using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Collections;
using Couchbase.Management.Query;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient;

public class StellarCollectionQueryIndexManagement
{
    private readonly ITestOutputHelper _outputHelper;
    public StellarCollectionQueryIndexManagement(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [InlineData("couchbase")]
    [InlineData("protostellar")]
    public async Task CreateAndDropCollectionIndex(string protocol)
    {
        var bucket = await StellarUtils.GetDefaultBucket(protocol).ConfigureAwait(false);
        var collectionManager = StellarUtils.GetDefaultBucket(protocol).Result.Collections;

        var scopeName = Guid.NewGuid().ToString();
        var collectionName = Guid.NewGuid().ToString();
        var collectionSpec = new CollectionSpec(scopeName, collectionName);

        try
        {
            await collectionManager.CreateScopeAsync(scopeName);
            await collectionManager.CreateCollectionAsync(collectionSpec);

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

                await collection.QueryIndexes.WatchIndexesAsync(new[] { indexName }, TimeSpan.FromMinutes(1),
                        new WatchQueryIndexOptions().CancellationToken(cts.Token))
                    .ConfigureAwait(false);

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
