#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Xunit;
using Couchbase.Stellar.Util;

namespace Couchbase.UnitTests.Stellar;

public class BucketTests
{
    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_DisposeAsync()
    {
        var bucket = await CreateBucket();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await bucket.DisposeAsync());
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_Dispose()
    {
        var bucket = await CreateBucket();
        Assert.Throws<UnsupportedInProtostellarException>(() => bucket.Dispose());
    }

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
        //Represents an unreachable host - the SDK will fail when the first op is called
        var connectionString = "couchbase2://xxx";

        var options = new ClusterOptions().WithCredentials("Administrator", "password");
        options.KvConnectTimeout = TimeSpan.FromMilliseconds(1);

        var cluster = await Cluster.ConnectAsync(connectionString, options);
        return await cluster.BucketAsync("default");
    }
}
#endif
