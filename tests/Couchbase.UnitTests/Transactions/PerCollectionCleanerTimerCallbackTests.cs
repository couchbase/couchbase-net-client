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
/// NCBC-4233. Drives <see cref="PerCollectionCleaner"/>'s timer callback through a
/// <see cref="FakeTimeProvider"/> and asserts the cycle completes rather than faults - the callback is
/// void-returning, so a fault there reaches a ThreadPool thread and takes the process down.
///
/// Nothing here waits on the wall clock: a fake timer given a zero due time fires synchronously inside
/// <see cref="PerCollectionCleaner.Start"/>.
/// </summary>
public class PerCollectionCleanerTimerCallbackTests
{
    private static readonly TimeSpan CleanupWindow = TimeSpan.FromSeconds(30);
    private static readonly Keyspace TestKeyspace = new("bkt", "scp", "col");

    /// <summary>
    /// A repository whose first call - the client-record read that opens every cycle - throws, so nothing
    /// downstream is reachable.
    /// </summary>
    private sealed class ThrowingRepository(Exception toThrow)
        : CleanerRepositoryBase(TestKeyspace, Mock.Of<ICluster>(), Mock.Of<ICouchbaseCollection>())
    {
        /// <summary>How many cycles got underway. A synchronously-completing async method hands back the
        /// cached <see cref="Task.CompletedTask"/>, so the Task itself cannot tell us the timer fired.</summary>
        public int CyclesStarted;

        public override Task<(ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas)> GetClientRecord(
            CancellationToken cancellationToken = default)
        {
            CyclesStarted++;
            throw toThrow;
        }

        public override Task CreatePlaceholderClientRecord(ulong? cas = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("unreachable");

        /// <summary>How many times the client record was removed - the last step of disposal, and the one
        /// that matters: leaving it behind keeps this client's ATRs assigned to a client that is gone.</summary>
        public int RemoveClientCalls;

        /// <summary>When set, the client-record removal fails. RemoveClient catches and retries, so this only
        /// becomes a fault disposal must survive when the sink it logs through is also down.</summary>
        public Exception? RemoveClientThrows;

        // Reached via DisposeAsync during shutdown; succeeding keeps RemoveClient's retry loop to one pass.
        public override Task RemoveClient(string clientUuid, DurabilityLevel durability = DurabilityLevel.None,
            CancellationToken cancellationToken = default)
        {
            RemoveClientCalls++;
            if (RemoveClientThrows is { } ex)
            {
                throw ex;
            }

            return Task.CompletedTask;
        }

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
    /// A logger that throws once armed - how a test reaches the outermost guard, since every handler in the
    /// cycle logs, so an armed sink makes a <c>catch</c> block itself throw. Not a contrivance: it is what a
    /// disposed logging provider does during shutdown.
    /// </summary>
    private sealed class ThrowingLoggerFactory : ILoggerFactory
    {
        public bool Armed;
        public int ThrowCount;

        /// <summary>Every message logged, with its level, recorded before the sink decides whether to throw.
        /// Match on the message as well as the level - several unrelated Warnings cross this path.</summary>
        public readonly List<(LogLevel Level, string Message)> Logged = new();

        /// <summary>
        /// When set, only this level throws. Needed to reach a specific handler: ProcessClient opens with a
        /// LogTrace, so a sink that throws on everything fails the cycle before it gets there.
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
                owner.Logged.Add((logLevel, formatter(state, exception)));
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
    /// Holds a disposal open where it removes the client record, so a test can observe a second caller
    /// arriving while the first is still in flight.
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

    /// <summary>Counts the first call a cycle makes, so a test can prove it saw the hooks it was given
    /// rather than the default ones.</summary>
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
    /// The timer is built from the injected TimeProvider, so a fake clock drives it. Guards against a timer
    /// that silently never fires.
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
    /// The collection-not-found handler records the reason and then logs. A throw from that log skips
    /// everything after the handler, so the teardown runs from a finally instead - otherwise the cleaner
    /// never disposes and retries a disowned collection once per window, forever.
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
    /// Disposal is idempotent under concurrency. Two callers exist in production and nothing orders them -
    /// the manager at shutdown, and a cycle disposing itself - and the body takes the callback mutex without
    /// releasing it, so a second concurrent run would block on it forever.
    ///
    /// The first disposal is held open at BeforeRemoveClient, so the second caller's arrival is observed
    /// rather than timed.
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
    /// Started the way <see cref="LostTransactionManager"/> does it - construct, wire up, then Start() - and
    /// the cycle still tears the cleaner down on a disowned collection.
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
    /// A dead log sink must not cut the teardown short. DisposeOnceAsync logs between every step, so unless
    /// those logs cannot throw, the first one skips the rest - including the client-record removal. An outer
    /// catch alone does not fix that: it makes the Task complete rather than fault, but the skipped steps
    /// stay skipped.
    ///
    /// Arms every Debug log, the level the breadcrumbs use. That also reaches the one unguarded Debug left
    /// on this path - RemoveClient's, from inside its own catch - so this fails if either fix is reverted.
    /// </summary>
    [Fact]
    public async Task ThrowingLogSink_DuringDisposal_StillRemovesTheClientRecord()
    {
        var timeProvider = new FakeTimeProvider();
        var loggerFactory = new ThrowingLoggerFactory();
        var (cleaner, repository) = Create(new InvalidOperationException("boom"), timeProvider,
            loggerFactory: loggerFactory);

        loggerFactory.OnlyLevel = LogLevel.Debug;
        loggerFactory.Armed = true;
        await cleaner.DisposeAsync();

        Assert.True(loggerFactory.ThrowCount >= 1, "expected the disposal breadcrumbs to have thrown");
        Assert.Equal(1, repository.RemoveClientCalls);
        Assert.Contains("Running = False", cleaner.ToString());
    }

    /// <summary>
    /// A disposal that fails is not re-raised at every later caller. DisposeAsync hands out one cached
    /// Task, and awaiting a faulted Task rethrows every time, so DisposeOnceAsync completes or logs and
    /// nothing else.
    ///
    /// Faults a step rather than a breadcrumb, since TryLog covers the breadcrumbs: the client-record
    /// removal fails and RemoveClient logs that failure through a sink that is also down. A throw from
    /// inside a <c>catch</c> escapes its retry loop, so nothing here waits on the clock.
    /// </summary>
    [Fact]
    public async Task FailedDisposal_IsNotRethrownToLaterCallers()
    {
        var timeProvider = new FakeTimeProvider();
        var loggerFactory = new ThrowingLoggerFactory();
        var (cleaner, repository) = Create(new InvalidOperationException("boom"), timeProvider,
            loggerFactory: loggerFactory);
        repository.RemoveClientThrows = new InvalidOperationException("client record removal failed");

        // Only RemoveClient's in-catch LogWarning throws; the Debug breadcrumbs are let through so the
        // teardown actually reaches that far.
        loggerFactory.OnlyLevel = LogLevel.Warning;
        loggerFactory.Armed = true;

        // The caller that hit the failure, and two that arrive afterwards onto the cached Task. Before
        // DisposeOnceAsync was made total, all three of these threw.
        await cleaner.DisposeAsync();
        await cleaner.DisposeAsync();
        await cleaner.DisposeAsync();

        Assert.True(loggerFactory.ThrowCount >= 1, "expected the in-catch log to have thrown");
        Assert.Equal(1, repository.RemoveClientCalls);
    }

    /// <summary>
    /// A disposal that did not complete is reported at Warning, not lost among the Debug breadcrumbs - a
    /// half-disposed cleaner leaves its client record behind.
    ///
    /// Arms the sink at Debug so RemoveClient's in-catch Debug log is what faults the teardown, leaving
    /// Warning working and therefore observable.
    /// </summary>
    [Fact]
    public async Task FailedDisposal_IsReportedAtWarning()
    {
        var timeProvider = new FakeTimeProvider();
        var loggerFactory = new ThrowingLoggerFactory();
        var (cleaner, repository) = Create(new InvalidOperationException("boom"), timeProvider,
            loggerFactory: loggerFactory);
        repository.RemoveClientThrows = new InvalidOperationException("client record removal failed");

        loggerFactory.OnlyLevel = LogLevel.Debug;
        loggerFactory.Armed = true;
        await cleaner.DisposeAsync();

        Assert.Contains(loggerFactory.Logged,
            entry => entry.Level == LogLevel.Warning && entry.Message.Contains("did not complete"));
    }

    /// <summary>
    /// The production interleaving: a cycle disposes itself on a disowned collection, then the manager
    /// disposes it again at shutdown and attaches to the first caller's cached Task.
    /// </summary>
    [Fact]
    public async Task FailedDisposal_DoesNotFaultTheSelfDisposingCleanupCycle()
    {
        var timeProvider = new FakeTimeProvider();
        var loggerFactory = new ThrowingLoggerFactory();
        var notified = 0;
        var (cleaner, repository) = Create(CollectionNotFoundTimeout(), timeProvider,
            onCollectionNotFound: _ => notified++, loggerFactory: loggerFactory);
        repository.RemoveClientThrows = new InvalidOperationException("client record removal failed");

        loggerFactory.OnlyLevel = LogLevel.Warning;
        loggerFactory.Armed = true;
        cleaner.Start();
        await cleaner.CurrentCycle;

        // The cycle disposed itself, the disposal failed, and neither fact escaped as a fault.
        Assert.Equal(TaskStatus.RanToCompletion, cleaner.CurrentCycle.Status);
        Assert.Equal(1, notified);
        Assert.Equal(1, repository.RemoveClientCalls);

        // And the manager disposing it at shutdown gets the same cached Task, not the failure again.
        await cleaner.DisposeAsync();
        Assert.Equal(1, repository.RemoveClientCalls);
    }

    /// <summary>
    /// The constructor must not start the timer. <see cref="LostTransactionManager"/> assigns TestHooks
    /// through an object initializer, after the constructor returns, and <c>TestHooks.BeforeGetRecord</c> is
    /// the first call a cycle makes - so a self-starting cleaner runs its first pass against the default
    /// hooks.
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
