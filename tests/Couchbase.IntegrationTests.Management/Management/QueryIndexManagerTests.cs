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
    public class QueryIndexManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public QueryIndexManagerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task CreateAndDropIndex()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(true);
            var bucketName = _fixture.GetDefaultBucket().Result.Name;

            const string indexName = "indexmgr_test";
            try
            {
                await cluster.QueryIndexes.CreateIndexAsync(
                    bucketName, indexName, new[] { "type" }).ConfigureAwait(true);
            }
            catch (IndexExistsException)
            {
                _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            bool failedCleanup = false;
            try
            {
                await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucketName).ConfigureAwait(true);

                using var cts = new CancellationTokenSource(10000);

                await cluster.QueryIndexes.WatchIndexesAsync(bucketName, new[] { indexName },
                    options => { options.CancellationToken(cts.Token); }).ConfigureAwait(true);

                var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucketName);
                Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
            }
            finally
            {
                try
                {
                    await cluster.QueryIndexes.DropIndexAsync(bucketName, indexName).ConfigureAwait(true);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Failure during cleanup: {e}");
                    failedCleanup = true;
                }
            }

            Assert.False(failedCleanup);
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task CreateAndDropCollectionIndex()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(true);
            var bucketName = _fixture.GetDefaultBucket().Result.Name;
            var collectionManager = _fixture.GetDefaultBucket().Result.Collections;

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
                    await cluster.QueryIndexes.CreateIndexAsync(bucketName, indexName, new[] { "type" }, options =>
                        {
                            options.ScopeNameValue = scopeName;
                            options.CollectionNameValue = collectionName;
                        }).ConfigureAwait(true);
                }
                catch (IndexExistsException)
                {
                    _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
                }

                var failedCleanup = false;
                try
                {
                    await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucketName, options =>
                    {
                        options.ScopeNameValue = scopeName;
                        options.CollectionNameValue = collectionName;
                    }).ConfigureAwait(true);

                    using var cts = new CancellationTokenSource(10000);

                    await cluster.QueryIndexes.WatchIndexesAsync(bucketName, new[] { indexName },
                        options =>
                        {
                            options.CancellationToken(cts.Token);
                            options.ScopeNameValue = scopeName;
                            options.CollectionNameValue = collectionName;
                        }).ConfigureAwait(true);

                    var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucketName, options =>
                    {
                        options.ScopeNameValue = scopeName;
                        options.CollectionNameValue = collectionName;
                    });
                    Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
                }
                finally
                {
                    try
                    {
                        await cluster.QueryIndexes.DropIndexAsync(bucketName, indexName, options =>
                        {
                            options.ScopeNameValue = scopeName;
                            options.CollectionNameValue = collectionName;
                        }).ConfigureAwait(true);
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

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task GetAllIndexesReturnsIndexesOnDefaultCollection()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(true);
            var bucketName = _fixture.GetDefaultBucket().Result.Name;

            var indexManager = cluster.QueryIndexes;

            try
            {
                await indexManager.CreatePrimaryIndexAsync(bucketName).ConfigureAwait(true);
            }
            catch (IndexExistsException)
            {
                //do nothing
            }

            var allIndexes = await indexManager.GetAllIndexesAsync(bucketName).ConfigureAwait(true);
            Assert.Single(allIndexes);

            allIndexes = await indexManager.GetAllIndexesAsync(bucketName,
                options => options.ScopeName("_default")).ConfigureAwait(true);
            Assert.Single(allIndexes);

            allIndexes = await indexManager.GetAllIndexesAsync(bucketName,
               options =>
               {
                   options.ScopeName("_default");
                   options.CollectionName("_default");
               }).ConfigureAwait(true);
            Assert.Single(allIndexes);
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task CreateIndexWithMissingField()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(true);
            var bucket = await _fixture.GetDefaultBucket();
            var indexManager = cluster.QueryIndexes;

            const string indexName = "idxCreateIndexWithMissingField_test";
            try
            {
                //CREATE INDEX idx4 ON default(age MISSING, body)
                await cluster.QueryIndexes.CreateIndexAsync(
                    bucket.Name, indexName, new[] { "age INCLUDE MISSING", "body" }).ConfigureAwait(true);
            }
            catch (IndexExistsException)
            {
                _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            bool failedCleanup = false;
            try
            {
                await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucket.Name).ConfigureAwait(true);

                using var cts = new CancellationTokenSource(10000);

                await cluster.QueryIndexes.WatchIndexesAsync(bucket.Name, new[] { indexName },
                    options => { options.CancellationToken(cts.Token); }).ConfigureAwait(true);

                var getIndexes = await cluster.QueryIndexes.GetAllIndexesAsync(bucket.Name);
                Assert.Contains(indexName, getIndexes.Select(idx => idx.Name));
            }
            finally
            {
                try
                {
                    await cluster.QueryIndexes.DropIndexAsync(bucket.Name, indexName).ConfigureAwait(true);
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
}
