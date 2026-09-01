#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Tests for the <see cref="LostTransactionManager"/> constructor's short-circuit when no cleanup
/// collections are configured. Previously an empty (non-null) collections list fell through the
/// <c>== null</c> guard into a <c>Task.Run(...).GetAwaiter().GetResult()</c> sync-over-async block;
/// a default cluster now returns without touching the cluster or blocking a thread (NCBC-4218).
/// </summary>
public class LostTransactionManagerTests
{
    // A strict cluster: any interaction would throw, proving the constructor short-circuits before
    // attempting to resolve/add any collection.
    private static ICluster StrictCluster => new Mock<ICluster>(MockBehavior.Strict).Object;

    private static LostTransactionManager Create(List<Keyspace>? collections) => new(
        StrictCluster,
        NullLoggerFactory.Instance,
        cleanupWindow: TimeSpan.FromSeconds(60),
        keyValueTimeout: TimeSpan.FromSeconds(1),
        collections: collections);

    [Fact]
    public async Task NullCollections_ShortCircuits_WithoutTouchingCluster()
    {
        var before = Create(null).CollectionsBeingCleaned.Count;

        // Construct again to confirm no collections were added as a side effect.
        var manager = Create(null);

        Assert.Equal(before, manager.CollectionsBeingCleaned.Count);
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task EmptyCollections_ShortCircuits_WithoutTouchingCluster()
    {
        var before = Create(new List<Keyspace>()).CollectionsBeingCleaned.Count;

        var manager = Create(new List<Keyspace>());

        Assert.Equal(before, manager.CollectionsBeingCleaned.Count);
        await manager.DisposeAsync();
    }

    /// <summary>
    /// Regression test: <c>CollectionsToClean</c> was previously
    /// <c>static</c>, shared by every <see cref="LostTransactionManager"/>
    /// in the process even though one is created per Cluster. Disposing any one manager (e.g. a short-lived
    /// Cluster opened just for a single test/operation) would tear down and remove another still-active
    /// Cluster's <see cref="PerCollectionCleaner"/> for a shared keyspace - silently killing that other Cluster's lost
    /// cleanup with no error anywhere. This is the mechanism behind lost-cleanup entries that are never
    /// picked up despite the cleaner logging clean, complete passes throughout.
    /// </summary>
    [Fact]
    public async Task DisposingOneManager_DoesNotAffectAnotherManagers_RegisteredCollections()
    {
        var keyspace = new Keyspace("bucket", "scope", "collection");

        var manager1 = Create(new List<Keyspace> { keyspace });
        var manager2 = Create(new List<Keyspace> { keyspace });

        Assert.Contains(keyspace, manager1.CollectionsBeingCleaned);
        Assert.Contains(keyspace, manager2.CollectionsBeingCleaned);

        await manager2.DisposeAsync();

        // manager2 disposing must not reach into manager1's (separate) set of collections being cleaned.
        Assert.Contains(keyspace, manager1.CollectionsBeingCleaned);

        await manager1.DisposeAsync();
    }
}
