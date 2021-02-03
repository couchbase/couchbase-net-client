using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests
{
    public class ClusterTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public ClusterTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Test_Open_More_Than_One_Bucket()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();

            var bucket1 = await cluster.BucketAsync("travel-sample").ConfigureAwait(false);
            Assert.NotNull(bucket1);

            var bucket2 = await cluster.BucketAsync("default").ConfigureAwait(false);
            Assert.NotNull(bucket2);

            try
            {
                var result1 = await (await bucket1.DefaultCollectionAsync()).InsertAsync(key, new {Whoah = "buddy!"})
                    .ConfigureAwait(false);

                var result2 = await (await bucket2.DefaultCollectionAsync()).InsertAsync(key, new {Whoah = "buddy!"})
                    .ConfigureAwait(false);
            }
            finally
            {
                await (await bucket1.DefaultCollectionAsync()).RemoveAsync(key).ConfigureAwait(false);
                await (await bucket2.DefaultCollectionAsync()).RemoveAsync(key).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Test_Query_With_Positional_Parameters()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var result = await cluster.QueryAsync<dynamic>("SELECT x.* FROM `default` WHERE x.Type=$1",
                options => { options.Parameter("foo"); }).ConfigureAwait(true);

            await foreach (var row in result)
            {
            }

            result.Dispose();
        }

        [Fact]
        public async Task Test_Query2()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var result = await cluster.QueryAsync<dynamic>("SELECT * FROM `default` WHERE type=$name;",
                options => { options.Parameter("name", "person"); }).ConfigureAwait(false);

            await foreach (var o in result)
            {
            }

            result.Dispose();
        }

        [Fact]
        public async Task Test_Views()
        {
            var cluster = _fixture.Cluster;
            var bucket = await cluster.BucketAsync("beer-sample").ConfigureAwait(false);

            var results = await bucket.ViewQueryAsync<object, object>("beer", "brewery_beers").ConfigureAwait(false);
            await foreach (var result in results)
            {
                _outputHelper.WriteLine($"id={result.Id},key={result.Key},value={result.Value}");
            }
        }

        [Fact]
        public async Task Test_WaitUntilReadyAsync()
        {
            var cluster = _fixture.Cluster;
            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_WaitUntilReadyAsync_Bucket()
        {
            var cluster = _fixture.Cluster;
            var defaultBucket = await cluster.BucketAsync("default").ConfigureAwait(false);
            await defaultBucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        }

        [Theory]
        [InlineData(ServiceType.KeyValue)]
        [InlineData(ServiceType.Views)]
        [InlineData(ServiceType.KeyValue, ServiceType.Views, ServiceType.Analytics, ServiceType.Query)]
        public async Task Test_WaitUntilReadyAsync_with_options(params ServiceType[] serviceTypes)
        {
            var cluster = _fixture.Cluster;
            var options = new WaitUntilReadyOptions()
            {
                CancellationTokenValue = CancellationToken.None,
                ServiceTypesValue = serviceTypes
            };

            await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(10), options).ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_Default_Scope_Collection_Async()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = await bucket.DefaultScopeAsync();
            var collection = await bucket.DefaultCollectionAsync();

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }

        [Fact]
        public async Task Test_Default_Scope_Collection_Legacy()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = bucket.DefaultScope();
            var collection = bucket.DefaultCollection();

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }

        [Fact]
        public async Task Test_Default_Open_Collection_From_Scope_Async()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = await bucket.DefaultScopeAsync();
            var collection = await scope.CollectionAsync("_default");

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }

        [Fact]
        public async Task Test_Default_Open_Collection_From_Scope_Legacy()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await cluster.BucketAsync("default");

            var scope = bucket.DefaultScope();
            var collection = scope.Collection("_default");

            Assert.Equal(Scope.DefaultScopeName, scope.Name);
            Assert.Equal(CouchbaseCollection.DefaultCollectionName, collection.Name);
        }
    }
}
