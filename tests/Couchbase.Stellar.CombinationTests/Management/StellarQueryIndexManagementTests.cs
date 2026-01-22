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
public class StellarQueryIndexManagementTests(
    StellarFixture fixture,
    ITestOutputHelper outputHelper)
{
    private readonly ConsistencyUtils _utils = new(fixture);

    [Fact]
    public async Task PrimaryIndexAlreadyExists()
    {
        var cluster = fixture.StellarCluster;

        try
        {
            await cluster.QueryIndexes.CreatePrimaryIndexAsync("default");
            var exception = await Record.ExceptionAsync(() => cluster.QueryIndexes.CreatePrimaryIndexAsync("default"))
                ;
            Assert.IsType<CollectionExistsException>(exception);
        }
        catch (IndexExistsException)
        {
            await cluster.QueryIndexes.DropPrimaryIndexAsync("default");
            Assert.True(true);
        }
    }

    [Fact]
    public async Task CreateAndDropIndex()
    {
        var cluster = fixture.StellarCluster;
        var bucket = await fixture.DefaultBucket();
        var bucketName = bucket.Name;

        const string indexName = "indexmgr_test";
        try
        {
            await cluster.QueryIndexes.CreateIndexAsync(
                bucketName, indexName, ["type"]);
        }
        catch (IndexExistsException)
        {
            outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
        }

        bool failedCleanup = false;
        try
        {
            await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucketName);

            using var cts = new CancellationTokenSource(10000);

            var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucketName);
            Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
        }
        finally
        {
            try
            {
                await cluster.QueryIndexes.DropIndexAsync(bucketName, indexName);
            }
            catch (Exception e)
            {
                outputHelper.WriteLine($"Failure during cleanup: {e}");
                failedCleanup = true;
            }
        }

        Assert.False(failedCleanup);
    }

    [Fact]
    public async Task CreateAndDropCollectionIndex()
    {
        var bucket = await fixture.DefaultBucket();
        var collectionManager = bucket.Collections;

        var scopeName = Guid.NewGuid().ToString();
        var collectionName = Guid.NewGuid().ToString();

        try
        {
            await collectionManager.CreateScopeAsync(scopeName);
            await _utils.WaitUntilScopeIsPresent(scopeName);
            await collectionManager.CreateCollectionAsync(scopeName, collectionName, new CreateCollectionSettings());
            await _utils.WaitUntilCollectionIsPresent(collectionName, scopeName: scopeName);

            const string indexName = "indexmgr_test_collection";
            try
            {
                var collection = await fixture.DefaultCollection();
                await collection.QueryIndexes.CreateIndexAsync(indexName, ["type"], new CreateQueryIndexOptions());
            }
            catch (IndexExistsException)
            {
                outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            var failedCleanup = false;
            try
            {
                var collection = await fixture.DefaultCollection();
                await collection.QueryIndexes.BuildDeferredIndexesAsync(
                    new BuildDeferredQueryIndexOptions());
                using var cts = new CancellationTokenSource(10000);

                var getIndexes = await collection.QueryIndexes.GetAllIndexesAsync(new GetAllQueryIndexOptions());
                Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
            }
            finally
            {
                try
                {
                    var collection = await fixture.DefaultCollection();
                    await collection.QueryIndexes.DropIndexAsync(indexName, new DropQueryIndexOptions());
                }
                catch (Exception e)
                {
                    outputHelper.WriteLine($"Failure during cleanup: {e}");
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

    [Fact]
    public async Task GetAllIndexesReturnsIndexesOnDefaultCollection()
    {
        var collection = await fixture.DefaultCollection();
        var indexManager = collection.QueryIndexes;

        try
        {
            await indexManager.CreatePrimaryIndexAsync(new CreatePrimaryQueryIndexOptions());
        }
        catch (IndexExistsException)
        {
            //do nothing
        }

        var allIndexes = await indexManager.GetAllIndexesAsync(new  GetAllQueryIndexOptions());
        Assert.Single(allIndexes);

        allIndexes = await indexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
        Assert.Single(allIndexes);

        allIndexes = await indexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
        Assert.Single(allIndexes);

        await indexManager.DropPrimaryIndexAsync(new  DropPrimaryQueryIndexOptions());
    }

    [Fact]
    public async Task CreateIndexWithMissingField()
    {
        var cluster = fixture.StellarCluster;
        var bucket = await fixture.DefaultBucket();

        const string indexName = "idxCreateIndexWithMissingField_test";
        try
        {
            //CREATE INDEX idx4 ON default(age MISSING, body)
            await cluster.QueryIndexes.CreateIndexAsync(
                bucket.Name, indexName, new[] { "age INCLUDE MISSING", "body" });
        }
        catch (IndexExistsException)
        {
            outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
        }

        bool failedCleanup = false;
        try
        {
            await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucket.Name);

            using var cts = new CancellationTokenSource(10000);

            var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucket.Name);
            Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
        }
        finally
        {
            try
            {
                await cluster.QueryIndexes.DropIndexAsync(bucket.Name, indexName);
            }
            catch (Exception e)
            {
                outputHelper.WriteLine($"Failure during cleanup: {e}");
                failedCleanup = true;
            }
        }

        Assert.False(failedCleanup);
    }
}
