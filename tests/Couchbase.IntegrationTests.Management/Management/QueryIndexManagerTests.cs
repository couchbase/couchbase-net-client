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
using Couchbase.Test.Common.Fixtures;
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
            var cluster = await _fixture.GetCluster();
            var bucket = await _fixture.GetDefaultBucket();
            var bucketName = bucket.Name;

            const string indexName = "indexmgr_test";
            try
            {
                await cluster.QueryIndexes.CreateIndexAsync(
                    bucketName, indexName, new[] { "type" });
            }
            catch (IndexExistsException)
            {
                _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            var failedCleanup = false;
            try
            {
                await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucketName);

                using var cts = new CancellationTokenSource(10000);

                await cluster.QueryIndexes.WatchIndexesAsync(bucketName, new[] { indexName },
                    options => { options.CancellationToken(cts.Token); });

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
                    _outputHelper.WriteLine($"Failure during cleanup: {e}");
                    failedCleanup = true;
                }
            }

            Assert.False(failedCleanup);
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task CreateAndDropCollectionIndex()
        {
            var cluster = await _fixture.GetCluster();
            var bucketName = _fixture.GetDefaultBucket().Result.Name;
            var collectionManager = _fixture.GetDefaultBucket().Result.Collections;

            var scopeName = Guid.NewGuid().ToString();
            var collectionName = Guid.NewGuid().ToString();
            var collectionSpec = new CollectionSpec(scopeName, collectionName);

            try
            {
                await collectionManager.CreateScopeAsync(scopeName);
                await collectionManager.CreateCollectionAsync(scopeName, collectionName, new CreateCollectionSettings());

                const string indexName = "indexmgr_test_collection";
                try
                {
                    await cluster.QueryIndexes.CreateIndexAsync(bucketName, indexName, new[] { "type" }, options =>
                        {
                            options.ScopeNameValue = scopeName;
                            options.CollectionNameValue = collectionName;
                        });
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
                    });

                    using var cts = new CancellationTokenSource(10000);

                    await cluster.QueryIndexes.WatchIndexesAsync(bucketName, new[] { indexName },
                        options =>
                        {
                            options.CancellationToken(cts.Token);
                            options.ScopeNameValue = scopeName;
                            options.CollectionNameValue = collectionName;
                        });

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
                        });
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
            var collection = await _fixture.GetDefaultCollectionAsync();
            var indexManager = collection.QueryIndexes;

            try
            {
                await indexManager.CreatePrimaryIndexAsync(new CreatePrimaryQueryIndexOptions());
            }
            catch (IndexExistsException)
            {
                //do nothing
            }

            var allIndexes = await indexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
            Assert.Single(allIndexes);

            allIndexes = await indexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
            Assert.Single(allIndexes);

            allIndexes = await indexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
            Assert.Single(allIndexes);
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task CreateIndexWithMissingField()
        {
            var cluster = await _fixture.GetCluster();
            var bucket = await _fixture.GetDefaultBucket();

            const string indexName = "idxCreateIndexWithMissingField_test";
            try
            {
                //CREATE INDEX idx4 ON default(age MISSING, body)
                await cluster.QueryIndexes.CreateIndexAsync(
                    bucket.Name, indexName, ["age INCLUDE MISSING", "body"]);
            }
            catch (IndexExistsException)
            {
                _outputHelper.WriteLine("IndexExistsException.  Maybe from a previous run.  Skipping.");
            }

            var failedCleanup = false;
            try
            {
                await cluster.QueryIndexes.BuildDeferredIndexesAsync(bucket.Name);

                using var cts = new CancellationTokenSource(10000);

                await cluster.QueryIndexes.WatchIndexesAsync(bucket.Name, new[] { indexName },
                    options => { options.CancellationToken(cts.Token); });

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
                    _outputHelper.WriteLine($"Failure during cleanup: {e}");
                    failedCleanup = true;
                }
            }

            Assert.False(failedCleanup);

        }
    }
}
