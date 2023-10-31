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

public class StellarQueryIndexManagement
{
    private readonly ITestOutputHelper _outputHelper;

    public StellarQueryIndexManagement(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    public async Task CreateAndDropIndex(string protocol)
    {
        var cluster = await StellarUtils.GetCluster(protocol).ConfigureAwait(false);
        var bucketName = StellarUtils.GetDefaultBucket(protocol).Result.Name;

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

            if (protocol != "protostellar") await cluster.QueryIndexes.WatchIndexesAsync(bucketName, new[] { indexName }, new WatchQueryIndexOptions()).ConfigureAwait(false);

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

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    public async Task CreateAndDropCollectionIndex(string protocol)
    {
        var cluster = await StellarUtils.GetCluster(protocol).ConfigureAwait(false);
        var bucketName = StellarUtils.GetDefaultBucket(protocol).Result.Name;
        var collectionManager = StellarUtils.GetDefaultBucket(protocol).Result.Collections;

        var scopeName = Guid.NewGuid().ToString();
        var collectionName = Guid.NewGuid().ToString();
        var collectionSpec = new CollectionSpec(scopeName, collectionName);

        try
        {
            await collectionManager.CreateScopeAsync(scopeName);
            await collectionManager.CreateCollectionAsync(collectionSpec);

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

                if (protocol != "protostellar")
                {
                    await cluster.QueryIndexes.WatchIndexesAsync(bucketName, new[] { indexName }, new WatchQueryIndexOptions().ScopeName(scopeName).CollectionName(collectionName)).ConfigureAwait(false);
                }

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

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    public async Task GetAllIndexesReturnsIndexesOnDefaultCollection(string protocol)
    {
        var cluster = await StellarUtils.GetCluster(protocol).ConfigureAwait(false);
        var bucketName = StellarUtils.GetDefaultBucket(protocol).Result.Name;

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

    [Theory]
    [InlineData("protostellar")]
    [InlineData("couchbase")]
    public async Task CreateIndexWithMissingField(string protocol)
    {
        var cluster = await StellarUtils.GetCluster(protocol).ConfigureAwait(false);
        var bucket = await StellarUtils.GetDefaultBucket(protocol);
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

            if (protocol != "protostellar")
            {
                await cluster.QueryIndexes.WatchIndexesAsync(bucket.Name, new[] { indexName }, options => { options.CancellationToken(cts.Token); }).ConfigureAwait(false);
            }

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
