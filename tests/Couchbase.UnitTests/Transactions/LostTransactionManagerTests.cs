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
}
