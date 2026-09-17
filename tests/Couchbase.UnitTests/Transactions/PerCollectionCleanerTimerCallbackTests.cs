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
using Couchbase.Client.Transactions.Internal.Test;
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
        Action<Keyspace>? onCollectionNotFound = null,
        ILoggerFactory? loggerFactory = null,
        bool start = false,
        TimeSpan? cleanupWindow = null)
    {
        var repository = new ThrowingRepository(thrownByRepository);
        var cleaner = new PerCollectionCleaner(
            clientUuid: "test-client",
            cleaner: new Cleaner(Mock.Of<ICluster>(), TimeSpan.FromSeconds(1), NullLoggerFactory.Instance),
            repository: repository,
            cleanupWindow: cleanupWindow ?? CleanupWindow,
            loggerFactory: loggerFactory ?? NullLoggerFactory.Instance,
            onCollectionNotFound: onCollectionNotFound,
            timeProvider: timeProvider);
        if (start)
        {
            cleaner.Start();
        }

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

        /// <summary>
        /// When set, only this level throws. Needed to reach a specific handler: ProcessClient opens with a
        /// LogTrace, so a sink that throws on everything fails the cycle there and every failure lands in
        /// the generic handler, never the one under test.
        /// </summary>
        public LogLevel? OnlyLevel;

        public ILogger CreateLogger(string categoryName) => new ThrowingLogger(this);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }

        private sealed class ThrowingLogger(ThrowingLoggerFactory owner) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!owner.Armed) return;
                if (owner.OnlyLevel is { } only && logLevel != only) return;
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

    /// <summary>
    /// Holds a disposal open at the point it goes to remove the client record, so a test can observe a
    /// second caller arriving while the first is still in flight - the interleaving that used to deadlock.
    /// </summary>
    private sealed class GatedRemoveClientHooks : DefaultCleanupTestHooks
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<int?> BeforeRemoveClient(string clientUuid)
        {
            Entered.TrySetResult(true);
            await _gate.Task.ConfigureAwait(false);
            return 1;
        }

        public void Release() => _gate.TrySetResult(true);
    }

    /// <summary>Counts the first call a cleanup cycle makes, so a test can prove the cycle saw the hooks it
    /// was given rather than the default ones.</summary>
    private sealed class CountingHooks : DefaultCleanupTestHooks
    {
        public int BeforeGetRecordCalls;

        public override Task<int?> BeforeGetRecord(string clientUuid)
        {
            BeforeGetRecordCalls++;
            return base.BeforeGetRecord(clientUuid);
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
        var (cleaner, _) = Create(new InvalidOperationException("boom"), timeProvider, loggerFactory: loggerFactory);

        // Arm only after construction, which logs on the way out.
        loggerFactory.Armed = true;

        cleaner.Start();
        await cleaner.CurrentCycle;

        Assert.Equal(TaskStatus.RanToCompletion, cleaner.CurrentCycle.Status);
        Assert.True(loggerFactory.ThrowCount >= 1, "expected the armed sink to have been hit");

        // Disarmed before teardown deliberately: logging on the disposal path is NOT exception-safe, and
        // is not meant to be. The cycle's outer guard contains a throwing sink; a disposal interrupted by
        // one is an accepted (and untested) gap - see the PR discussion on LogSafe.
        loggerFactory.Armed = false;
        await cleaner.DisposeAsync();
    }

    /// <summary>
    /// A throwing sink must not leak the timer-callback mutex. Every handler used to log *before* calling
    /// TryReleaseMutex, so a sink that threw escaped mid-handler and the semaphore was never released
    /// again - later callbacks then timed out on it and DisposeAsync's untimed wait blocked forever.
    /// Release now happens in a finally.
    /// </summary>
    [Fact]
    public async Task ThrowingLogSink_DoesNotLeakTheCallbackMutex()
    {
        var timeProvider = new FakeTimeProvider();
        var loggerFactory = new ThrowingLoggerFactory();
        var (cleaner, _) = Create(new InvalidOperationException("boom"), timeProvider, loggerFactory: loggerFactory);

        Assert.Equal(1, cleaner.CallbackMutexCount);

        loggerFactory.Armed = true;
        cleaner.Start();
        await cleaner.CurrentCycle;

        // Handed back despite the sink throwing inside the handler that was reporting the failure.
        Assert.Equal(1, cleaner.CallbackMutexCount);

        // And DisposeAsync, which waits on that same mutex without a timeout, still returns rather than
        // hanging - which is what this test is really guarding. (Disarmed first: disposal-path logging is
        // not exception-safe, and this test is about the mutex, not about that.)
        loggerFactory.Armed = false;
        await cleaner.DisposeAsync();
    }

    /// <summary>
    /// The collection-not-found handler records the reason and then logs. If that log throws, the
    /// exception propagates straight out to the outer guard, so anything after the handler is skipped -
    /// which used to mean the cleaner never disposed and went on retrying a collection the server had
    /// disowned, once per window, forever. The teardown now runs from a finally, so the reason being
    /// recorded is enough.
    /// </summary>
    [Fact]
    public async Task ThrowingLogSink_StillDisposesOnCollectionNotFound()
    {
        var timeProvider = new FakeTimeProvider();
        var loggerFactory = new ThrowingLoggerFactory();
        var notified = 0;
        var (cleaner, _) = Create(CollectionNotFoundTimeout(), timeProvider,
            onCollectionNotFound: _ => notified++, loggerFactory: loggerFactory);

        // Only the handler's own LogWarning throws; everything before it is allowed through so the cycle
        // actually reaches the collection-not-found path.
        loggerFactory.OnlyLevel = LogLevel.Warning;
        loggerFactory.Armed = true;
        cleaner.Start();
        await cleaner.CurrentCycle;

        Assert.True(loggerFactory.ThrowCount >= 1, "expected the handler's own log to have thrown");
        Assert.Equal(TaskStatus.RanToCompletion, cleaner.CurrentCycle.Status);

        // The notification still fired and the cleaner stopped itself, despite the handler that decided
        // both of those throwing on its way out. (No mutex assertion here: a completed teardown leaves the
        // count at 0 by design - DisposeAsync takes it and never hands it back, so that no further
        // callback can run.)
        Assert.Equal(1, notified);
        Assert.Contains("Running = False", cleaner.ToString());
    }

    /// <summary>
    /// Disposal is idempotent under concurrency. The guard used to be an unsynchronised check-then-act, so
    /// two callers could both enter the body; since it takes the callback mutex and never releases it, the
    /// loser blocked on that semaphore forever. That hang propagated out through
    /// LostTransactionManager's Task.WhenAll to Transactions.DisposeAsync, so cluster teardown never
    /// returned. Both callers exist in production and nothing orders them: the manager disposes every
    /// cleaner at shutdown while a cleanup cycle can dispose itself.
    ///
    /// No wall-clock waits: the first disposal is held open at BeforeRemoveClient, so the second caller's
    /// arrival is observed directly rather than timed.
    /// </summary>
    [Fact]
    public async Task ConcurrentDisposal_BothCallersComplete()
    {
        var timeProvider = new FakeTimeProvider();
        var hooks = new GatedRemoveClientHooks();
        var (cleaner, _) = Create(new InvalidOperationException("boom"), timeProvider);
        cleaner.TestHooks = hooks;

        // First caller: runs until it is parked inside RemoveClient, still holding the callback mutex.
        var first = cleaner.DisposeAsync().AsTask();
        await hooks.Entered.Task;
        Assert.False(first.IsCompleted);

        // Second caller arrives mid-disposal. It must attach to the same run, not block on the mutex the
        // first one is holding and will never hand back.
        var second = cleaner.DisposeAsync().AsTask();
        Assert.False(second.IsCompleted);

        hooks.Release();
        await Task.WhenAll(first, second);

        // And a caller arriving after the fact still returns, as it always did.
        await cleaner.DisposeAsync();
    }

    /// <summary>
    /// Started through the helper rather than by hand, which is the shape
    /// <see cref="LostTransactionManager"/> uses: construct, wire up, then Start(). The cleanup cycle still
    /// runs end to end and still tears the cleaner down on a disowned collection.
    /// </summary>
    [Fact]
    public async Task StartedAfterConstruction_StillDisposesOnCollectionNotFound()
    {
        var timeProvider = new FakeTimeProvider();
        var notified = 0;
        var (cleaner, repository) = Create(CollectionNotFoundTimeout(), timeProvider,
            onCollectionNotFound: _ => notified++, start: true);

        await cleaner.CurrentCycle;

        Assert.Equal(1, repository.CyclesStarted);
        Assert.Equal(TaskStatus.RanToCompletion, cleaner.CurrentCycle.Status);
        Assert.Equal(1, notified);
        Assert.Contains("Running = False", cleaner.ToString());
    }

    /// <summary>
    /// The constructor must not start the timer. <see cref="LostTransactionManager"/> assigns TestHooks
    /// through an object initializer - so after the constructor has returned - and the first thing a cleanup
    /// cycle does is call <c>TestHooks.BeforeGetRecord</c>. A cleaner that armed its own timer would run that
    /// first cycle against the default hooks: guaranteed under a fake clock, which fires the callback
    /// synchronously inside the call that arms it, and a ThreadPool race against the real one.
    /// </summary>
    [Fact]
    public async Task ConstructorDoesNotStartTheTimer_SoHooksWiredAfterwardsAreSeen()
    {
        var timeProvider = new FakeTimeProvider();
        var hooks = new CountingHooks();
        var (cleaner, repository) = Create(new InvalidOperationException("boom"), timeProvider);
        await using var _ = cleaner;

        // Nothing has run yet: construction alone must not arm the timer.
        Assert.Equal(0, repository.CyclesStarted);

        cleaner.TestHooks = hooks;
        cleaner.Start();
        await cleaner.CurrentCycle;

        Assert.Equal(1, repository.CyclesStarted);
        Assert.Equal(1, hooks.BeforeGetRecordCalls);
    }
}
