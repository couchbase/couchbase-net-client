#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Cleanup;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using TimeoutException = Couchbase.Core.Exceptions.TimeoutException;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// NCBC-4233. <see cref="PerCollectionCleaner"/>'s timer callback used to be <c>async void</c>, so anything
/// escaping it was re-thrown on a ThreadPool thread with no SynchronizationContext to catch it - which
/// terminates the process. The body's catch-all did not cover this: an exception thrown from inside a
/// <c>catch</c> block is not caught by a sibling <c>catch</c>, and two of the handlers go on to do real work
/// (invoke a caller-supplied callback, await DisposeAsync).
///
/// These tests drive the callback through a <see cref="FakeTimeProvider"/> and assert the resulting cycle
/// completes rather than faults. Nothing here waits on the wall clock: a fake timer given a zero due time
/// fires synchronously inside <see cref="PerCollectionCleaner.Start"/>.
/// </summary>
public class PerCollectionCleanerTimerCallbackTests
{
    private static readonly TimeSpan CleanupWindow = TimeSpan.FromSeconds(30);
    private static readonly Keyspace TestKeyspace = new("bkt", "scp", "col");

    /// <summary>
    /// A repository whose very first call - the client-record read that opens every cleanup cycle - throws.
    /// Everything downstream (ATR lookup, the Cleaner) is therefore unreachable.
    /// </summary>
    private sealed class ThrowingRepository(Exception toThrow)
        : CleanerRepositoryBase(TestKeyspace, Mock.Of<ICluster>(), Mock.Of<ICouchbaseCollection>())
    {
        /// <summary>How many cleanup cycles actually got underway. The cycles here run to completion
        /// synchronously, and a synchronously-completing async Task method hands back the cached
        /// <see cref="Task.CompletedTask"/> instance - so the Task itself cannot tell us the timer fired.</summary>
        public int CyclesStarted;

        public override Task<(ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas)> GetClientRecord(
            CancellationToken cancellationToken = default)
        {
            CyclesStarted++;
            throw toThrow;
        }

        public override Task CreatePlaceholderClientRecord(ulong? cas = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unreachable");

        // Reached via DisposeAsync during shutdown; succeeding keeps RemoveClient's retry loop to one pass.
        public override Task RemoveClient(string clientUuid, DurabilityLevel durability = DurabilityLevel.None,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public override Task UpdateClientRecord(string clientUuid, TimeSpan cleanupWindow, int numAtrs,
            IReadOnlyList<string> expiredClientIds, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unreachable");

        public override Task<(Dictionary<string, AtrEntry> attempts, ParsedHLC? parsedHlc)> LookupAttempts(
            string atrId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unreachable");
    }

    private static (PerCollectionCleaner Cleaner, ThrowingRepository Repository) Create(
        Exception thrownByRepository,
        FakeTimeProvider timeProvider,
        Action<Keyspace>? onCollectionNotFound = null)
    {
        var repository = new ThrowingRepository(thrownByRepository);
        var cleaner = new PerCollectionCleaner(
            clientUuid: "test-client",
            cleaner: new Cleaner(Mock.Of<ICluster>(), TimeSpan.FromSeconds(1), NullLoggerFactory.Instance),
            repository: repository,
            cleanupWindow: CleanupWindow,
            loggerFactory: NullLoggerFactory.Instance,
            startDisabled: true,
            onCollectionNotFound: onCollectionNotFound,
            timeProvider: timeProvider);
        return (cleaner, repository);
    }

    /// <summary>
    /// A logger that throws once armed. This is how a test reaches the outermost guard: the handlers inside
    /// the cycle all log, so an armed sink makes a <c>catch</c> block itself throw - which is precisely the
    /// shape that used to escape the <c>async void</c> and take the process down. A sink that throws is not a
    /// contrivance either; it is what a disposed or misconfigured logging provider does during shutdown.
    /// </summary>
    private sealed class ThrowingLoggerFactory : ILoggerFactory
    {
        public bool Armed;
        public int ThrowCount;

        public ILogger CreateLogger(string categoryName) => new ThrowingLogger(this);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        private sealed class ThrowingLogger(ThrowingLoggerFactory owner) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!owner.Armed) return;
                owner.ThrowCount++;
                throw new InvalidOperationException("logging sink is down");
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private static TimeoutException CollectionNotFoundTimeout() =>
        new(new KeyValueErrorContext { RetryReasons = new List<RetryReason> { RetryReason.CollectionNotFound } });

    /// <summary>
    /// The timer is built from the injected TimeProvider, so a fake clock drives it. Guards against the
    /// TimeProvider swap silently producing a timer that never fires.
    /// </summary>
    [Fact]
    public async Task TimerFiresFromTheInjectedTimeProvider()
    {
        var timeProvider = new FakeTimeProvider();
        var (cleaner, repository) = Create(new InvalidOperationException("boom"), timeProvider);
        await using var _ = cleaner;

        Assert.Equal(0, repository.CyclesStarted);

        cleaner.Start();
        await cleaner.CurrentCycle;
        Assert.Equal(1, repository.CyclesStarted);

        // And it is periodic, not one-shot.
        timeProvider.Advance(CleanupWindow);
        await cleaner.CurrentCycle;
        Assert.Equal(2, repository.CyclesStarted);
    }

    /// <summary>
    /// The pre-existing catch-all is still reachable after the async void split.
    /// </summary>
    [Fact]
    public async Task RepositoryThrows_CycleCompletesWithoutFaulting()
    {
        var timeProvider = new FakeTimeProvider();
        var (cleaner, repository) = Create(new InvalidOperationException("boom"), timeProvider);
        await using var _ = cleaner;

        cleaner.Start();
        await cleaner.CurrentCycle;

        Assert.Equal(1, repository.CyclesStarted);
        Assert.Equal(TaskStatus.RanToCompletion, cleaner.CurrentCycle.Status);
    }

    /// <summary>
    /// The regression test for NCBC-4233. The collection-not-found handler invokes a caller-supplied callback;
    /// when that throws, the exception used to escape the async void and kill the process. It must now be
    /// contained, and - because losing the disposal would just trade a crash for a cleaner that retries a
    /// disowned collection forever - the handler must still dispose.
    /// </summary>
    [Fact]
    public async Task OnCollectionNotFoundThrows_CycleCompletesAndStillDisposes()
    {
        var timeProvider = new FakeTimeProvider();
        var notified = 0;
        var (cleaner, repository) = Create(
            CollectionNotFoundTimeout(),
            timeProvider,
            onCollectionNotFound: _ =>
            {
                notified++;
                throw new InvalidOperationException("notification sink threw");
            });

        cleaner.Start();
        await cleaner.CurrentCycle;

        Assert.Equal(1, repository.CyclesStarted);
        Assert.Equal(TaskStatus.RanToCompletion, cleaner.CurrentCycle.Status);
        Assert.Equal(1, notified);

        // And the throwing callback did not cost us the disposal that follows it - Running is reported
        // through ToString(), and is false only once the cancellation token has been tripped by Stop().
        Assert.Contains("Running = False", cleaner.ToString());
    }

    /// <summary>
    /// The outermost guard. With an armed logging sink every <c>catch</c> block in the cycle throws as it
    /// tries to report, so nothing inside <c>CleanupCycleCoreAsync</c> can contain the failure - this is the
    /// case the old <c>async void</c> signature turned into a process kill. The guard's own log call throws
    /// too, which is why it is wrapped in a bare <c>catch</c>.
    /// </summary>
    [Fact]
    public async Task ThrowingLogSink_CycleCompletesWithoutFaulting()
    {
        var timeProvider = new FakeTimeProvider();
        var loggerFactory = new ThrowingLoggerFactory();
        var cleaner = new PerCollectionCleaner(
            clientUuid: "test-client",
            cleaner: new Cleaner(Mock.Of<ICluster>(), TimeSpan.FromSeconds(1), NullLoggerFactory.Instance),
            repository: new ThrowingRepository(new InvalidOperationException("boom")),
            cleanupWindow: CleanupWindow,
            loggerFactory: loggerFactory,
            startDisabled: true,
            timeProvider: timeProvider);

        // Arm only after construction, which logs on the way out.
        loggerFactory.Armed = true;

        cleaner.Start();
        await cleaner.CurrentCycle;

        Assert.Equal(TaskStatus.RanToCompletion, cleaner.CurrentCycle.Status);
        // Both the handler's log attempt and the outer guard's own attempt threw.
        Assert.True(loggerFactory.ThrowCount >= 2, $"expected the sink to be hit at least twice, was {loggerFactory.ThrowCount}");
    }
}
