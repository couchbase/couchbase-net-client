#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Query;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Stellar;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Search;
using Couchbase.Stellar.Util;
using Couchbase.Stellar.Query;
using Couchbase.Utils;
using Couchbase.Core.Exceptions;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.VisualBasic;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar;

public class BucketTests
{
    [Fact]
    public async Task Throw_FeatureNotAvailableException_ViewQueryAsync()
    {
        var bucket = await CreateBucket();
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => await bucket.ViewQueryAsync<object, string>("", ""));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_PingAsync()
    {
        var bucket = await CreateBucket();
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => await bucket.PingAsync());
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_ViewIndexes()
    {
        var bucket = await CreateBucket();
        Assert.Throws<FeatureNotAvailableException>(() => bucket.ViewIndexes);
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_WaitUntileReadyAsync()
    {
        var bucket = await CreateBucket();
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => await bucket.WaitUntilReadyAsync(TimeSpan.Zero));
    }

    private async Task<IBucket> CreateBucket()
    {
        return await CreateClusterFromMocks().BucketAsync("default");
    }

    [Fact]
    public async Task Scopes_Are_Cached()
    {
        var bucket = await CreateBucket();
        var scope = bucket.Scope("scope1");
        var scope1 = bucket.Scope("scope1");

        Assert.Equal(scope.GetHashCode(), scope1.GetHashCode());
    }

    [Fact]
    public async Task Collections_Are_Cached()
    {
        var bucket = await CreateBucket();
        var collection = bucket.Scope("scope1").Collection("coll1");
        var collection1 = bucket.Scope("scope1").Collection("coll1");

        Assert.Equal(collection.GetHashCode(), collection1.GetHashCode());
    }


    [Fact]
    public async Task Dispose_Is_Idempotent()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();
        bucket.Dispose();//no side effect
    }

    [Fact]
    public async Task Throw_ODE_When_DefaultCollectionAsync_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async ()=> await bucket.DefaultCollectionAsync());
    }

    [Fact]
    public async Task Throw_ODE_When_ScopeAsync_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async ()=> await bucket.ScopeAsync("name"));
    }

    [Fact]
    public async Task Throw_ODE_When_Scope_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        Assert.Throws<ObjectDisposedException>(()=> bucket.Scope("name"));
    }

    [Fact]
    public async Task  Throw_ODE_When_DefaultCollection_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        Assert.Throws<ObjectDisposedException>(()=> bucket.DefaultCollection());
    }

    [Fact]
    public async Task Throw_ODE_When_Collection_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        Assert.Throws<ObjectDisposedException>(()=> bucket.Collection("name"));
    }

    [Fact]
    public async Task Throw_ODE_When_Collections_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        Assert.Throws<ObjectDisposedException>(()=> bucket.Collections);
    }

    [Fact]
    public async Task Throw_ODE_When_DefaultScope_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        Assert.Throws<ObjectDisposedException>(()=> bucket.DefaultScope());
    }

    [Fact]
    public async Task Throw_ODE_When_DefaultScopeAsync_Called_After_Being_Disposed()
    {
        var cluster = CreateClusterFromMocks();
        var bucket = await cluster.BucketAsync("default");

        bucket.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(async ()=> await bucket.DefaultScopeAsync());
    }

    internal StellarCluster CreateClusterFromMocks()
    {
        var channel = GrpcChannel.ForAddress(new Uri("https://xxx"));
        var bucketManager = new Mock<IBucketManager>();
        var searchIndexManager = new Mock<ISearchIndexManager>();
        var queryServiceClient = new Mock<IQueryClient>();
        var analyticsClient = new Mock<IAnalyticsClient>();
        var searchClient = new Mock<IStellarSearchClient>();
        var queryIndexManager = new Mock<IQueryIndexManager>();
        var metaData = new Metadata();
        var requestTracer = new Mock<IRequestTracer>();
        var typeSerializer = new Mock<ITypeSerializer>();
        var clusterOptions = new ClusterOptions();
        var channelCredentials = new ClusterChannelCredentials(clusterOptions);
        var requestOrchestrator = new Mock<IRetryOrchestrator>();

        return new StellarCluster(bucketManager.Object, searchIndexManager.Object, queryIndexManager.Object,
            queryServiceClient.Object, analyticsClient.Object, searchClient.Object, metaData, channelCredentials,
            requestTracer.Object, channel, typeSerializer.Object, requestOrchestrator.Object, clusterOptions);
    }
}
#endif
