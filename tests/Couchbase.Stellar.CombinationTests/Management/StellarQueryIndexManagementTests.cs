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
public class StellarQueryIndexManagementTests
{
    private readonly ITestOutputHelper _outputHelper;
    private StellarFixture _fixture;
    private ConsistencyUtils _utils;
    public StellarQueryIndexManagementTests(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
        _utils = new ConsistencyUtils(fixture);
    }

    [Fact]
    public async Task CreateAndDropIndex()
    {
        var cluster = _fixture.StellarCluster;
        var bucketName = _fixture.DefaultBucket().Result.Name;

        const string indexName = "indexmgr_test";
        try
        {
            await cluster.QueryIndexes.CreateIndexAsync(
                bucketName, indexName, new[] { "type" }).ConfigureAwait(false);
        }
        catch (IndexExistsException)
        {
            _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
        }

        bool failedCleanup = false;
        try
        {
            await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucketName).ConfigureAwait(false);

            using var cts = new CancellationTokenSource(10000);

            var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucketName);
            Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
        }
        finally
        {
            try
            {
                await cluster.QueryIndexes.DropIndexAsync(bucketName, indexName).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _outputHelper.WriteLine($"Failure during cleanup: {e}");
                failedCleanup = true;
            }
        }

        Assert.False(failedCleanup);
    }

    [Fact]
    public async Task CreateAndDropCollectionIndex()
    {
        var cluster = _fixture.StellarCluster;
        var bucketName = _fixture.DefaultBucket().Result.Name;
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


            const string indexName = "indexmgr_test_collection";
            try
            {
                await cluster.QueryIndexes.CreateIndexAsync(bucketName, indexName, new[] { "type" }, new CreateQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName)).ConfigureAwait(false);
            }
            catch (IndexExistsException)
            {
                _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            var failedCleanup = false;
            try
            {
                await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucketName, new BuildDeferredQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName)).ConfigureAwait(false);

                using var cts = new CancellationTokenSource(10000);

                var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucketName, new GetAllQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName));
                Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
            }
            finally
            {
                try
                {
                    await cluster.QueryIndexes.DropIndexAsync(bucketName, indexName, new DropQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName)).ConfigureAwait(false);
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

    [Fact]
    public async Task GetAllIndexesReturnsIndexesOnDefaultCollection()
    {
        var cluster = _fixture.StellarCluster;
        var bucketName = _fixture.DefaultBucket().Result.Name;

        var indexManager = cluster.QueryIndexes;

        try
        {
            await indexManager.CreatePrimaryIndexAsync(bucketName).ConfigureAwait(false);
        }
        catch (IndexExistsException)
        {
            //do nothing
        }

        var allIndexes = await indexManager.GetAllIndexesAsync(bucketName).ConfigureAwait(false);
        Assert.Single(allIndexes);

        allIndexes = await indexManager.GetAllIndexesAsync(bucketName,
            options => options.ScopeName("_default")).ConfigureAwait(false);
        Assert.Single(allIndexes);

        allIndexes = await indexManager.GetAllIndexesAsync(bucketName,
           options =>
           {
               options.ScopeName("_default");
               options.CollectionName("_default");
           }).ConfigureAwait(false);
        Assert.Single(allIndexes);

        await indexManager.DropPrimaryIndexAsync(bucketName).ConfigureAwait(false);
    }

    [Fact]
    public async Task CreateIndexWithMissingField()
    {
        var cluster = _fixture.StellarCluster;
        var bucket = await _fixture.DefaultBucket();
        var indexManager = cluster.QueryIndexes;

        const string indexName = "idxCreateIndexWithMissingField_test";
        try
        {
            //CREATE INDEX idx4 ON default(age MISSING, body)
            await cluster.QueryIndexes.CreateIndexAsync(
                bucket.Name, indexName, new[] { "age INCLUDE MISSING", "body" }).ConfigureAwait(false);
        }
        catch (IndexExistsException)
        {
            _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
        }

        bool failedCleanup = false;
        try
        {
            await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucket.Name).ConfigureAwait(false);

            using var cts = new CancellationTokenSource(10000);

            var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucket.Name);
            Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
        }
        finally
        {
            try
            {
                await cluster.QueryIndexes.DropIndexAsync(bucket.Name, indexName).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _outputHelper.WriteLine($"Failure during cleanup: {e}");
                failedCleanup = true;
            }
        }

        Assert.False(failedCleanup);
    }
}
