#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Stellar;
using Couchbase.Stellar.Util;
using Xunit;

namespace Couchbase.UnitTests.Stellar.KeyValue;

public class CollectionTests
{
    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.RemoveAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_RemoveAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.RemoveAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_UpsertAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.UpsertAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_UnlockAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.UnlockAsync("key", 010101));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_ExistsAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.ExistsAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_TouchAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.TouchAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_LookupInAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.LookupInAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAndLockAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.GetAndLockAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAndTouchAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.GetAndTouchAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_TryGetAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.TryGetAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_MutateInAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.MutateInAsync("key", new List<MutateInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_ReplaceAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.ReplaceAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_InsertAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => await collection.InsertAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_ScanAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async() => collection.ScanAsync(new PrefixScan("prefix")));
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_GetAnyReplicaAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await collection.GetAnyReplicaAsync("key"));
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_LookUpInAnyReplicaAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => await collection.LookupInAnyReplicaAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_UnsupportedInProtostellarException_LookUpInAllReplicasAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<UnsupportedInProtostellarException>(async () => collection.LookupInAllReplicasAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAllReplicasAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<AggregateException>(async () => collection.GetAllReplicasAsync("key"));
    }

    private async Task<ICouchbaseCollection> CreateCollection()
    {
        //Represents an unreachable host - the SDK will fail when the first op is called
        var connectionString = "couchbase2://xxx";

        var options = new ClusterOptions().WithCredentials("Administrator", "password");
        options.KvConnectTimeout = TimeSpan.FromMilliseconds(1);

        var cluster = await Cluster.ConnectAsync(connectionString, options);
        var bucket = await cluster.BucketAsync("default");
        return bucket.DefaultCollection();
    }
}
#endif
