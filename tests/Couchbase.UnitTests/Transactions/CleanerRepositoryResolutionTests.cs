#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Tests for <see cref="CleanerRepositoryBase"/>'s lazy collection resolution (NCBC-4218 follow-up): a
/// configured cleanup collection is resolved on demand and cached on success, retried on failure, and
/// skipped entirely when a resolved collection is supplied up front (the dynamic path).
/// </summary>
public class CleanerRepositoryResolutionTests
{
    private static readonly Keyspace SampleKeyspace = new("default", "_default", "_default");

    // Minimal concrete repository exposing only the resolution behavior; the abstract data-access
    // methods are not exercised here.
    private sealed class TestRepository : CleanerRepositoryBase
    {
        public TestRepository(Keyspace keyspace, ICluster cluster, ICouchbaseCollection? resolved = null)
            : base(keyspace, cluster, resolved) { }

        public override Task<(ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas)> GetClientRecord(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override Task CreatePlaceholderClientRecord(ulong? cas = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task RemoveClient(string clientUuid, DurabilityLevel durability = DurabilityLevel.None, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task UpdateClientRecord(string clientUuid, TimeSpan cleanupWindow, int numAtrs, IReadOnlyList<string> expiredClientIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public override Task<(Dictionary<string, AtrEntry> attempts, ParsedHLC? parsedHlc)> LookupAttempts(string atrId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private static (Mock<ICluster> cluster, ICouchbaseCollection collection) MockResolvableCluster()
    {
        var collection = Mock.Of<ICouchbaseCollection>();
        var scope = new Mock<IScope>();
        scope.Setup(s => s.CollectionAsync(SampleKeyspace.CollectionName)).Returns(new ValueTask<ICouchbaseCollection>(collection));
        var bucket = new Mock<IBucket>();
        bucket.Setup(b => b.ScopeAsync(SampleKeyspace.ScopeName)).Returns(new ValueTask<IScope>(scope.Object));
        var cluster = new Mock<ICluster>();
        cluster.Setup(c => c.BucketAsync(SampleKeyspace.BucketName)).Returns(new ValueTask<IBucket>(bucket.Object));
        return (cluster, collection);
    }

    [Fact]
    public async Task GetCollectionAsync_ResolvesViaCluster()
    {
        var (cluster, collection) = MockResolvableCluster();
        var repo = new TestRepository(SampleKeyspace, cluster.Object);

        Assert.Same(collection, await repo.GetCollectionAsync());
    }

    [Fact]
    public async Task GetCollectionAsync_CachesOnSuccess()
    {
        var (cluster, _) = MockResolvableCluster();
        var repo = new TestRepository(SampleKeyspace, cluster.Object);

        await repo.GetCollectionAsync();
        await repo.GetCollectionAsync();

        cluster.Verify(c => c.BucketAsync(SampleKeyspace.BucketName), Times.Once);
    }

    [Fact]
    public async Task GetCollectionAsync_DoesNotCacheFailure_RetriesUntilSuccess()
    {
        var collection = Mock.Of<ICouchbaseCollection>();
        var scope = new Mock<IScope>();
        scope.Setup(s => s.CollectionAsync(SampleKeyspace.CollectionName)).Returns(new ValueTask<ICouchbaseCollection>(collection));
        var bucket = new Mock<IBucket>();
        bucket.Setup(b => b.ScopeAsync(SampleKeyspace.ScopeName)).Returns(new ValueTask<IScope>(scope.Object));

        var cluster = new Mock<ICluster>();
        var calls = 0;
        cluster.Setup(c => c.BucketAsync(SampleKeyspace.BucketName))
            .Returns(() =>
            {
                calls++;
                // First attempt fails (e.g. bucket still warming up); second succeeds.
                return calls == 1
                    ? throw new Couchbase.Core.Exceptions.BucketNotFoundException(SampleKeyspace.BucketName)
                    : new ValueTask<IBucket>(bucket.Object);
            });

        var repo = new TestRepository(SampleKeyspace, cluster.Object);

        await Assert.ThrowsAsync<Couchbase.Core.Exceptions.BucketNotFoundException>(() => repo.GetCollectionAsync());
        // A failed resolve is not cached, so a later attempt succeeds.
        Assert.Same(collection, await repo.GetCollectionAsync());
        cluster.Verify(c => c.BucketAsync(SampleKeyspace.BucketName), Times.Exactly(2));
    }

    [Fact]
    public async Task GetCollectionAsync_SeededCollection_SkipsResolution()
    {
        var seeded = Mock.Of<ICouchbaseCollection>();
        var cluster = new Mock<ICluster>(MockBehavior.Strict); // any cluster call would throw
        var repo = new TestRepository(SampleKeyspace, cluster.Object, resolved: seeded);

        Assert.Same(seeded, await repo.GetCollectionAsync());
        cluster.Verify(c => c.BucketAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCollectionAsync_CancelledToken_ThrowsWithoutResolving()
    {
        var cluster = new Mock<ICluster>(MockBehavior.Strict); // any cluster call would throw
        var repo = new TestRepository(SampleKeyspace, cluster.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => repo.GetCollectionAsync(cts.Token));
        cluster.Verify(c => c.BucketAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetCollectionAsync_CancelledToken_DoesNotCacheSoLaterAttemptSucceeds()
    {
        var (cluster, collection) = MockResolvableCluster();
        var repo = new TestRepository(SampleKeyspace, cluster.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => repo.GetCollectionAsync(cts.Token));
        // Cancellation must not poison the cache: a later, uncancelled attempt still resolves.
        Assert.Same(collection, await repo.GetCollectionAsync());
    }
}
