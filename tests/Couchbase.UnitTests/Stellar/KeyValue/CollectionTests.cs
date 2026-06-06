#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Stellar.KeyValue;

/// <summary>
/// Shared cluster/collection for <see cref="CollectionTests"/>. Every test drives a KV op against
/// the same unreachable "couchbase2://xxx" host, so there is no reason to build a cluster per test:
/// the connection string and options never vary and the cluster never connects. One cluster is
/// created once and disposed once here, which also tears down the GrpcChannel and its
/// SocketsHttpHandler (and keep-alive ping timers) that would otherwise leak.
/// </summary>
public sealed class StellarCollectionFixture : IAsyncLifetime
{
    private ICluster _cluster = null!;

    public ICouchbaseCollection Collection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Represents an unreachable host - the SDK will fail when the first op is called.
        var connectionString = "couchbase2://xxx";

#pragma warning disable CS0618 // Type or member is obsolete
        var options = new ClusterOptions().WithCredentials("Administrator", "password");
#pragma warning restore CS0618 // Type or member is obsolete
        // These tests drive KV ops against an unreachable host; without a short KvTimeout each
        // failing op would block on the default 2.5s.
        options.WithFastFailTimeouts(FastFailServices.Kv);

        _cluster = await Cluster.ConnectAsync(connectionString, options);
        var bucket = await _cluster.BucketAsync("default");
        Collection = bucket.DefaultCollection();
    }

    public async Task DisposeAsync() => await _cluster.DisposeAsync();
}

public class CollectionTests : IClassFixture<StellarCollectionFixture>
{
    private readonly ICouchbaseCollection _collection;

    public CollectionTests(StellarCollectionFixture fixture) => _collection = fixture.Collection;

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.RemoveAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_RemoveAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.RemoveAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_UpsertAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.UpsertAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_UnlockAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.UnlockAsync("key", 010101));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_ExistsAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.ExistsAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_TouchAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.TouchAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_LookupInAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.LookupInAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAndLockAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.GetAndLockAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAndTouchAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.GetAndTouchAsync("key", TimeSpan.Zero));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_TryGetAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.TryGetAsync("key"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_MutateInAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.MutateInAsync("key", new List<MutateInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_ReplaceAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.ReplaceAsync("key", "value"));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_InsertAsync()
    {
        await Assert.ThrowsAnyAsync<CouchbaseException>(async () => await _collection.InsertAsync("key", "value"));
    }

    [Fact]
    public void Throw_FeatureNotAvailableException_ScanAsync()
    {
        Assert.Throws<FeatureNotAvailableException>(() => _collection.ScanAsync(new PrefixScan("prefix")));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_GetAnyReplicaAsync()
    {
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => await _collection.GetAnyReplicaAsync("key"));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_LookUpInAnyReplicaAsync()
    {
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => await _collection.LookupInAnyReplicaAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_FeatureNotAvailableException_LookUpInAllReplicasAsync()
    {
        await Assert.ThrowsAsync<FeatureNotAvailableException>(async () => _collection.LookupInAllReplicasAsync("key", new List<LookupInSpec>()));
    }

    [Fact]
    public async Task Throw_Exception_When_ClusterConnectAsync_Fails_GetAllReplicasAsync()
    {
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            foreach (var task in _collection.GetAllReplicasAsync("key"))
            {
                await task;
            }
        });
    }
}
#endif
