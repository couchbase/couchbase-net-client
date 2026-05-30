#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Stellar;
using Couchbase.Stellar.Util;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Stellar.KeyValue;

public class CollectionTests
{
    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.RemoveAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_RemoveAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.RemoveAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_UpsertAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.UpsertAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_UnlockAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.UnlockAsync("key", 010101));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_ExistsAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.ExistsAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_TouchAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.TouchAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_LookupInAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.LookupInAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAndLockAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.GetAndLockAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAndTouchAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.GetAndTouchAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_TryGetAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.TryGetAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_MutateInAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.MutateInAsync("key", new List<MutateInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_ReplaceAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.ReplaceAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_InsertAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await collection.InsertAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_ScanAsync()
    {
        var collection = await CreateCollection();
        Assert.Throws<FeatureNotAvailableException>(() => collection.ScanAsync(new PrefixScan("prefix")));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_GetAnyReplicaAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => await collection.GetAnyReplicaAsync("key"));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_LookUpInAnyReplicaAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => await collection.LookupInAnyReplicaAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_LookUpInAllReplicasAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => collection.LookupInAllReplicasAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAllReplicasAsync()
    {
        var collection = await CreateCollection();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            foreach (var task in collection.GetAllReplicasAsync("key"))
            {
                await task;
            }
        });
    }

    private async Task<ICouchbaseCollection> CreateCollection()
    {
        //Represents an unreachable host - the SDK will fail when the first op is called
        var connectionString = "couchbase2://xxx";

#pragma warning disable CS0618 // Type or member is obsolete
        var options = new ClusterOptions().WithCredentials("Administrator", "password");
#pragma warning restore CS0618 // Type or member is obsolete
        // All tests using this helper drive KV ops against an unreachable host; without a short
        // KvTimeout each failing op would block on the default 2.5s.
        options.WithFastFailTimeouts(FastFailServices.Kv);

        var cluster = await Cluster.ConnectAsync(connectionString, options);
        var bucket = await cluster.BucketAsync("default");
        return bucket.DefaultCollection();
    }
}
#endif
