using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Cleanup;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.Support;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Tests for <see cref="CleanupWorkQueue"/> after its conversion from a thread-blocking
/// <c>BlockingCollection.GetConsumingEnumerable()</c> consumer to a <c>Channel&lt;T&gt;</c> +
/// <c>await foreach</c> consumer that yields its thread when the queue is idle (NCBC-4218).
/// </summary>
public class CleanupWorkQueueTests
{
    private static CleanupWorkQueue CreateQueue(bool runCleanup) =>
        new(Mock.Of<ICluster>(), keyValueTimeout: TimeSpan.FromSeconds(1), NullLoggerFactory.Instance, runCleanup);

    private static CleanupRequest MakeRequest(string atrId = "atr-1") => new(
        AttemptId: Guid.NewGuid().ToString(),
        AtrId: atrId,
        AtrCollection: Mock.Of<ICouchbaseCollection>(),
        InsertedIds: new List<DocRecord>(),
        ReplacedIds: new List<DocRecord>(),
        RemovedIds: new List<DocRecord>(),
        State: AttemptStates.NOTHING_WRITTEN,
        WhenReadyToBeProcessed: DateTimeOffset.UtcNow,
        ProcessingErrors: new ConcurrentQueue<Exception>());

    [Fact]
    public void TryAddCleanupRequest_EnqueuesAndQueueLengthReflectsContents()
    {
        // runCleanup: false means no consumer drains the queue, so we can observe its contents.
        using var queue = CreateQueue(runCleanup: false);

        Assert.True(queue.TryAddCleanupRequest(MakeRequest("a")));
        Assert.True(queue.TryAddCleanupRequest(MakeRequest("b")));

        Assert.Equal(2, queue.QueueLength);
    }

    [Fact]
    public void RemainingCleanupRequests_DrainsQueuedItems()
    {
        using var queue = CreateQueue(runCleanup: false);
        queue.TryAddCleanupRequest(MakeRequest("a"));
        queue.TryAddCleanupRequest(MakeRequest("b"));

        var remaining = queue.RemainingCleanupRequests.ToList();

        Assert.Equal(new[] { "a", "b" }, remaining.Select(r => r.AtrId));
        Assert.Equal(0, queue.QueueLength); // draining removes them
    }

    [Fact]
    public async Task ForceFlushAsync_OnIdleConsumer_Completes()
    {
        // The whole point of the Channel conversion: a running-but-idle consumer awaits
        // asynchronously and unwinds when asked to stop, rather than being stuck blocking a
        // thread-pool thread. Await directly rather than racing a timer — a wall-clock assertion
        // would be flaky on thread-starved CI, while a genuine hang is caught by the test runner.
        using var queue = CreateQueue(runCleanup: true);

        await queue.ForceFlushAsync();
    }

    [Fact]
    public async Task ForceFlushAsync_WithRunCleanupFalse_Completes()
    {
        using var queue = CreateQueue(runCleanup: false);

        await queue.ForceFlushAsync();
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var queue = CreateQueue(runCleanup: true);

        queue.Dispose();
        var ex = Record.Exception(() => queue.Dispose());

        Assert.Null(ex);
    }
}
