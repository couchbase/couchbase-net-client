#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Stellar;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Search;
using Couchbase.Stellar.Util;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.VisualBasic;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar;

public class BucketTests
{
    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_ViewQueryAsync()
    {
        var bucket = await CreateBucket();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await bucket.ViewQueryAsync<object, string>("", ""));
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_PingAsync()
    {
        var bucket = await CreateBucket();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await bucket.PingAsync());
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_ViewIndexes()
    {
        var bucket = await CreateBucket();
        Assert.Throws<UnsupportedInProtostellarException>(() => bucket.ViewIndexes);
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_WaitUntileReadyAsync()
    {
        var bucket = await CreateBucket();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await bucket.WaitUntilReadyAsync(TimeSpan.Zero));
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

    internal StellarCluster CreateClusterFromMocks()
    {
        var channel = GrpcChannel.ForAddress(new Uri("https://xxx"));
        var bucketManager = new Mock<IBucketManager>();
        var searchIndexManager = new Mock<ISearchIndexManager>();
        var queryServiceClient = new Mock<QueryService.QueryServiceClient>();
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
