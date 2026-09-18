#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.Internal;
using Couchbase.Client.Transactions.Internal.Test;
using Couchbase.Client.Transactions.Support;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Cleanup.LostTransactions
{
    internal class PerCollectionCleaner : IAsyncDisposable
    {
        public string ClientUuid { get; }

        private readonly Cleaner _cleaner;
        private readonly CleanerRepositoryBase _repository;
        private readonly TimeSpan _cleanupWindow;
        private readonly ILogger<PerCollectionCleaner> _logger;
        private readonly ITimer _processCleanupTimer;
        private readonly Random _jitter = new Random();
        private readonly SemaphoreSlim _timerCallbackMutex = new (1);
        private readonly Action<Keyspace>? _onCollectionNotFound;
        private long _runCount;
        private readonly TimeProvider _timeProvider;
        // ATRs still to clean on the current lap, plus the lap-scoped schedule state. Persists across cleanup
        // windows so an unfinished lap resumes (rather than restarting) on the next pass. Only ever touched from
        // ProcessClient, which the timer-callback mutex serializes - so no concurrent collection is needed.
        private readonly AtrCleanupQueue _atrsToClean;
        private readonly CancellationTokenSource _cancelToken = new ();
        // Lazy's default thread-safety mode runs the factory once under a lock and hands every other caller
        // the same Task - which is exactly the "dispose once, everyone awaits it" contract wanted here.
        private readonly Lazy<Task> _disposal;


        public ICleanupTestHooks TestHooks { get; set; } = DefaultCleanupTestHooks.Instance;
        public long RunCount => Interlocked.Read(ref _runCount);
        private bool Running => !_cancelToken.IsCancellationRequested;

        // Never starts the timer. The caller calls Start() once it has finished wiring the cleaner up -
        // notably TestHooks, which is assigned through an object initializer after this returns and which
        // the first cleanup cycle reads on its very first call.
        public PerCollectionCleaner(string clientUuid, Cleaner cleaner, CleanerRepositoryBase repository,TimeSpan cleanupWindow, ILoggerFactory loggerFactory, Action<Keyspace>? onCollectionNotFound = null, TimeProvider? timeProvider = null)
        {
            ClientUuid = clientUuid;
            _cleaner = cleaner; // TODO: Cleaner should have its data access refactored into CleanerRepositoryBase, and then that should be made a property, eliminating the need for a _repository variable here.
            _repository = repository;
            _cleanupWindow = cleanupWindow;
            _onCollectionNotFound = onCollectionNotFound;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _disposal = new Lazy<Task>(DisposeOnceAsync);
            _atrsToClean = new AtrCleanupQueue(_timeProvider);
            _logger = loggerFactory.CreateLogger<PerCollectionCleaner>();
            // Every log message in the cleanup callback reads this, so it is assigned before anything could
            // arm the timer.
            FullBucketName = (bucket: BucketName, scope: ScopeName, collection: CollectionName, clientUuid: ClientUuid).ToString();
            // Driven by the injected TimeProvider so a test can advance a FakeTimeProvider instead of waiting
            // on the wall clock, and with ExecutionContext flow suppressed: this timer lives for as long as the
            // cleaner does, and the SDK is lazy-initialized, so without suppression the AsyncLocals in scope at
            // bootstrap (logging scopes, the first HttpContext, activity tracing) would be pinned for the
            // lifetime of the process. Every other long-lived periodic timer in the SDK does the same.
            //
            // Always created disabled - nothing here arms it. A zero due time would let the provider invoke
            // the callback before this very assignment completes (synchronously, inside CreateTimer, on a fake
            // clock; on a ThreadPool thread with the real one), and the callback's auth and
            // collection-not-found paths reach Stop() through this field.
            _processCleanupTimer = TimerFactory.CreateWithFlowSuppressed(
                _timeProvider,
                callback: TimerCallback,
                state: null,
                dueTime: Timeout.InfiniteTimeSpan,
                period: cleanupWindow);

            _logger.LogInformation("Created PerCollectionCleaner on '{coll}'", repository.Keyspace);
        }

        public void Start()
        {
            _processCleanupTimer.Change(TimeSpan.Zero, _cleanupWindow);
        }

        // Only ever called from DisposeOnceAsync, so this log is on the teardown path too and goes through
        // TryLog for the same reason the ones there do: a dead sink here would skip everything after it.
        public void Stop()
        {
            _processCleanupTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _cancelToken.Cancel();
            TryLog("Cancelling per collection cleaner for {clientUuid}", ClientUuid);
        }

        public string BucketName => _repository.BucketName;
        public string ScopeName => _repository.ScopeName;
        public string CollectionName => _repository.CollectionName;

        public string FullBucketName { get; }

        public override string ToString()
        {
            return new Summary(FullBucketName, ClientUuid, Running, RunCount).ToString();
        }

        private record Summary(string FullBucketName, string ClientUuid, bool Running, long RunCount);

        // Disposal runs once, and every caller awaits that same run.
        //
        // This used to guard on _cancelToken.IsCancellationRequested - an unsynchronised check-then-act.
        // Two callers could both pass it, and because the body below takes _timerCallbackMutex and
        // deliberately never gives it back, the loser blocked on that semaphore forever. It was awaited by
        // LostTransactionManager.RemoveClientEntries inside a Task.WhenAll, so the hang propagated all the
        // way out to Transactions.DisposeAsync and cluster teardown never completed.
        //
        // Both callers are real and nothing orders them: LostTransactionManager disposes every cleaner at
        // shutdown, while a cleanup cycle disposes itself on an auth error or on a collection the server
        // has disowned. The only interleaving whose behaviour changes here is the one that used to deadlock
        // - a caller arriving after disposal already finished returned immediately before, and still does.
        public ValueTask DisposeAsync() => new(_disposal.Value);

        // Total by construction: this must never fault, for the same reason RunCleanupCycleAsync must not -
        // but by a different route. The Task returned here is the one _disposal caches, and awaiting a
        // faulted Task rethrows every time, so a single failed teardown would be re-raised at every later
        // caller forever. (Lazy's own exception caching is not what does this: the factory is an async
        // method, which never throws synchronously, so Lazy always gets a Task and caches that. The
        // observable behaviour is the same either way.)
        //
        // Swallowing costs nothing the callers were using: LostTransactionManager.RemoveClientEntries
        // already catches and logs around its Task.WhenAll, and the self-disposing cleanup cycle discards
        // this into RunCleanupCycleAsync's guard. The warning below just says which collection it was.
        private async Task DisposeOnceAsync()
        {
            // Yield before touching anything. An async method runs synchronously until its first await that
            // actually yields, and this one is a Lazy value factory - so without this, everything down to the
            // first incomplete await executes while Lazy's recursion guard is armed. Stop() below cancels a
            // token whose registrations run inline on this thread, which can resume a parked cleanup cycle
            // here; if that cycle then unwinds to its own teardown (the auth path does, with no await on the
            // way), it re-enters _disposal.Value on this very thread and Lazy throws. Yielding first hands the
            // Task back immediately, so a re-entrant caller simply awaits the disposal already in flight.
            await Task.Yield();

            try
            {
                // Every breadcrumb between the teardown steps goes through TryLog. Not being total is only
                // half the problem a throwing sink causes here: an outer catch alone would still let a dead
                // sink skip the steps below it, trading a loud half-disposed cleaner for a quiet one. With
                // nothing throwable between them, the sequence always runs to the end.
                TryLog("Disposing of PerCollectionCleaner for {bkt}", FullBucketName);
                Stop();
                // at this point, there will be no more timer callbacks triggered, so lets
                // wait for the mutex, at which point the current ProcessClient (if any) is
                // done (and there will be no more).
                TryLog("waiting for cleanup Task on {bkt}", FullBucketName);
                await  _timerCallbackMutex.WaitAsync().CAF();
                TryLog("cleanup Task stopped for {bkt}", FullBucketName);
                _processCleanupTimer.Dispose();
                TryLog("cleanup Timer stopped for {bkt}", FullBucketName);
                await RemoveClient().CAF();
                TryLog("removed ClientRecord for {bkt}", FullBucketName);
                _cancelToken.Dispose();
            }
            catch (Exception ex)
            {
                // The steps themselves can still throw where TryLog cannot help: Timer.Change and
                // CancellationTokenSource.Cancel/Dispose can raise ObjectDisposedException, Cancel surfaces
                // anything an inline registration throws, and RemoveClient logs from inside its own catch
                // blocks.
                TryLog("Disposal of '{bkt}' did not complete: {ex}", FullBucketName, ex);
            }
        }

        // Logging must never be the thing that defeats disposal. A logging provider disposed before the SDK
        // is the likely way that happens, and disposal is exactly when that race is on.
        //
        // Deliberately silent in the catch: the sink we would report the failure to is the one that just
        // failed. Debug level because every caller is a breadcrumb, and the one thing worth seeing at a
        // higher level - a teardown that did not finish - is logged through here too and will simply be lost
        // along with the rest if the sink is dead.
        private void TryLog(string message, params object?[] args)
        {
            try
            {
                _logger.LogDebug(message, args);
            }
            catch
            {
                // Intentionally empty - see above.
            }
        }

        // Releasing the timer-callback mutex must never mask the cleanup that follows it: the auth-error
        // and collection-not-found paths release and then await DisposeAsync (which re-acquires it), so a
        // throw from Release (e.g. SemaphoreFullException, ObjectDisposedException) would skip disposal.
        // Swallow it - the only contract this mutex enforces is "one ProcessClient at a time".
        private void TryReleaseMutex()
        {
            try
            {
                _timerCallbackMutex.Release();
            }
            catch (Exception ex)
            {
                // Through TryLog: this catch exists so the teardown that follows the release still runs, and
                // a throwing sink here would defeat exactly that.
                TryLog("Ignoring error releasing timer callback mutex on {bkt}: {ex}", FullBucketName, ex);
            }
        }

        // The Timer hands us a void-returning callback with nobody to give a Task to, so this adapter is the
        // last place an exception can be observed. Previously this method was `async void`: anything escaping
        // it was re-thrown on a ThreadPool thread with no SynchronizationContext to catch it, which terminates
        // the process. That was reachable - an exception thrown from inside a catch block is not caught by a
        // sibling catch, and both the auth-error and collection-not-found handlers below go on to await
        // DisposeAsync (which can throw ObjectDisposedException when the owning LostTransactionManager is
        // tearing the cleaner down concurrently) and invoke a caller-supplied callback.
        //
        // Discarding the Task here is safe only because RunCleanupCycleAsync is total - see below.
        private void TimerCallback(object? state) => CurrentCycle = RunCleanupCycleAsync();

        /// <summary>
        /// The cleanup cycle currently in flight, or a completed Task. Production code synchronizes on
        /// <see cref="_timerCallbackMutex"/>; this exists so a test driving a fake <see cref="TimeProvider"/>
        /// can await a cycle rather than poll the wall clock. Overlapping callbacks racing on this assignment
        /// is benign: the mutex serializes the work itself, and a test advances one tick at a time.
        /// </summary>
        internal Task CurrentCycle { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Free slots on the timer-callback mutex: 1 when no cycle holds it, 0 while one does (or, if the
        /// release were ever missed, forever after). Exists so a test can assert the release happened - a
        /// leaked slot has no other visible effect until a later callback times out on it, or
        /// <see cref="DisposeAsync"/> blocks on it without a timeout and never returns.
        /// </summary>
        internal int CallbackMutexCount => _timerCallbackMutex.CurrentCount;

        // Total by construction: this must never fault, because nothing observes the Task it returns.
        private async Task RunCleanupCycleAsync()
        {
            try
            {
                await CleanupCycleCoreAsync().CAF();
            }
            catch (Exception ex)
            {
                try
                {
                    _logger.LogWarning("Cleanup cycle on '{bkt}' failed unexpectedly: {ex}", FullBucketName, ex);
                }
                catch
                {
                    // A throwing logger sink must not be the thing that brings the process down.
                }
            }
        }

        private async Task CleanupCycleCoreAsync()
        {
            if (_cancelToken.IsCancellationRequested)
            {
                _logger.LogDebug($"{ClientUuid} TimerCallback after already disposed.");
                // could be we notice this before setting the task completion token so set it here just in case
                return;
            }

            var enteredWithoutTimeout = await _timerCallbackMutex.WaitAsync(_cleanupWindow).CAF();
            if (!enteredWithoutTimeout)
            {
                _logger.LogDebug("Timed out while waiting for overlapping callbacks on {bkt}", FullBucketName);
                return;
            }

            // The handlers below only record why we are stopping; the teardown runs from the outer finally,
            // because DisposeAsync re-acquires the mutex this cycle still holds.
            //
            // Both invariants live in finally blocks rather than at the end of the happy path, because a
            // handler can itself throw - its own log call is the likely culprit, a logging provider being
            // disposed during shutdown - and that exception propagates straight out to RunCleanupCycleAsync,
            // which now swallows it. Anything after such a handler is therefore not guaranteed to run unless
            // it is in a finally: the mutex would leak, and a cleaner that has already decided to stop would
            // carry on retrying a collection the server disowned, once per window, forever.
            var stop = StopReason.None;
            try
            {
                try
                {
                    _ = await ProcessClient().CAF();
                }
                catch (AuthenticationFailureException)
                {
                    // BF-CBD-3794
                    stop = StopReason.AuthFailure;
                    _logger.LogDebug("Exiting cleanup of '{bkt}' due to access error", FullBucketName);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown: the cancellation token tripped mid-cycle (a pending
                    // Task.Delay or an early ThrowIfCancellationRequested in CleanupAtr). Exit quietly.
                    _logger.LogDebug("Cleanup of '{bkt}' cancelled during shutdown.", FullBucketName);
                }
                catch (Exception ex) when (IsCollectionNotFound(ex))
                {
                    // The server doesn't recognize this collection (deleted, or a misconfigured keyspace
                    // that never existed) - stop cleaning it.
                    stop = StopReason.CollectionNotFound;
                    _logger.LogWarning("Stopping lost cleanup of '{bkt}': collection not found (deleted or misconfigured).", FullBucketName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Processing of bucket '{bkt}' failed unexpectedly: {ex}", FullBucketName, ex);
                }
                finally
                {
                    TryReleaseMutex();
                }
            }
            finally
            {
                if (stop != StopReason.None)
                {
                    try
                    {
                        if (stop == StopReason.CollectionNotFound)
                        {
                            // Caller-supplied (LostTransactionManager logs in it), so not assumed safe.
                            _onCollectionNotFound?.Invoke(_repository.Keyspace);
                        }
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning("Collection-not-found notification for '{bkt}' threw: {ex}", FullBucketName, notifyEx);
                    }
                    finally
                    {
                        // Reached whatever the notification or its own logging did, and once reached,
                        // DisposeOnceAsync performs every step: nothing between them can throw any more. A
                        // dead sink can still cost us the _cancelToken.Dispose() at the very end - RemoveClient
                        // logs from inside its own catch blocks, which escapes it - but that is after the
                        // client record is gone, so no invariant rides on it.
                        await DisposeAsync().CAF();
                    }
                }
            }
        }

        // Why the cycle stopped, when that means tearing the cleaner down. Recorded rather than acted on
        // inline so the teardown runs after the mutex has been released.
        private enum StopReason
        {
            None,
            AuthFailure,
            CollectionNotFound,
        }

        // True only when a cleanup op timed out and the SOLE retry reason was collection/scope not found
        // (server returned UnknownCollection/UnknownScope). Mixed reasons (network, etc.) keep retrying.
        // The server can't distinguish a deleted collection from one that never existed.
        internal static bool IsCollectionNotFound(Exception ex)
        {
            if (ex is not Couchbase.Core.Exceptions.TimeoutException timeout)
            {
                return false;
            }

            var reasons = timeout.Context?.RetryReasons;
            if (reasons is null || reasons.Count == 0)
            {
                return false;
            }

            return reasons.All(static r =>
                r is Couchbase.Core.Retry.RetryReason.CollectionNotFound
                  or Couchbase.Core.Retry.RetryReason.ScopeNotFound);
        }

        // method referred to as "Per Bucket Algo" in the RFC
        internal async Task<ClientRecordDetails> ProcessClient(bool cleanupAtrs = true)
        {
            _logger.LogTrace("Looking for lost transactions on bucket '{bkt}'", FullBucketName);
            ClientRecordDetails clientRecordDetails = await EnsureClientRecordIsUpToDate().CAF();
            if (_cancelToken.IsCancellationRequested)
            {
                _logger.LogDebug($"{ClientUuid} Process Client cancelled.");
                return clientRecordDetails;
            }
            if (clientRecordDetails.OverrideActive)
            {
                _logger.LogInformation("Cleanup of '{bkt}' is currently disabled by another actor.", FullBucketName);
                return clientRecordDetails;
            }

            // for fit tests, we may want to manipulate the client records without actually bothering with cleanup.
            if (cleanupAtrs)
            {
                // Process this client's assigned ATRs as a "lap" - one complete pass in deterministic order, evenly
                // paced across the cleanup window. Adaptive batching scales the batch size up when we fall behind
                // so we still try to clean the whole lap within the window. If even that can't keep up (a too-short
                // window, or a CPU-starved host), we stop at the window boundary rather than overrunning it: the
                // remainder stays queued and resumes on the next pass, so every ATR is eventually cleaned and no
                // tail is starved. Bounding each pass by the window also keeps this client's heartbeat fresh (it is
                // refreshed once per pass, above), avoiding spurious expiry and ATR reassignment by its peers.
                var totalAtrs = clientRecordDetails.AtrsHandledByThisClient.Count;
                var batchProcessor = new AtrBatchProcessor(_cleanupWindow, totalAtrs, _timeProvider);

                // Start a fresh lap, or resume one a previous (window-bounded) pass left unfinished. Also rebuilds
                // the lap if the topology changed and this client's slice of ATRs is now different.
                _atrsToClean.SyncLap(
                    clientRecordDetails.AtrsHandledByThisClient,
                    clientRecordDetails.IndexOfThisClient,
                    clientRecordDetails.NumActiveClients);

                var passStartTimestamp = _timeProvider.GetTimestamp();

                while (!_cancelToken.IsCancellationRequested
                       && _timeProvider.GetElapsedTime(passStartTimestamp) < _cleanupWindow
                       && !_atrsToClean.LapComplete)
                {
                    var batchStartTimestamp = _timeProvider.GetTimestamp();

                    // Batch size is driven by lap-scoped elapsed/progress (which persist across windows), so a lap
                    // resumed from a previous pass reports itself as behind from its first batch and sprints.
                    var batchSize = batchProcessor.CalculateBatchSize(_atrsToClean.LapElapsed, _atrsToClean.LapProcessed);

                    // Take up to batchSize distinct ATRs from the queue. TakeBatch never refills, so a batch only
                    // ever holds distinct ids - they can be cleaned concurrently without double-cleaning one ATR.
                    var batch = _atrsToClean.TakeBatch(batchSize);
                    if (batch.Count == 0)
                    {
                        // Nothing is assigned to this client (empty lap).
                        _logger.LogWarning("No ATRs handled by this client?");
                        break;
                    }

                    // Process batch (parallel if > 1)
                    await AtrBatchProcessor.ProcessBatchAsync(batch, CleanupAtr, _cancelToken.Token).CAF();

                    _atrsToClean.RecordCleaned(batch.Count);
                    Interlocked.Add(ref _runCount, batch.Count);

                    var batchDuration = _timeProvider.GetElapsedTime(batchStartTimestamp);
                    _logger.LogTrace("Cleaned {count} ATRs in {elapsed}ms (batch size: {batchSize}) on {bkt}",
                        batch.Count, batchDuration.TotalMilliseconds, batchSize, FullBucketName);

                    // Delay to maintain schedule (returns immediately when behind, letting the batching sprint).
                    await batchProcessor.ApplyDelayAsync(batch.Count, batchDuration, _cancelToken.Token).CAF();
                }

                _logger.LogDebug("Cleanup pass on {bkt}: {processed}/{total} ATRs done this lap, {remaining} remaining, {elapsedMs}ms elapsed in pass.",
                    FullBucketName, _atrsToClean.LapProcessed, totalAtrs, _atrsToClean.Remaining,
                    _timeProvider.GetElapsedTime(passStartTimestamp).TotalMilliseconds);
            }
            return clientRecordDetails;
        }

        private async Task<ClientRecordDetails> EnsureClientRecordIsUpToDate()
        {
            ClientRecordDetails? clientRecordDetails = null;
            bool repeat;
            do
            {
                ulong? pathnotFoundCas = null;
                try
                {
                    // Parse the client record.
                    await TestHooks.BeforeGetRecord(ClientUuid).CAF();
                    (ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas) = await _repository.GetClientRecord(_cancelToken.Token).CAF();
                    if (clientRecord == null)
                    {
                        _logger.LogDebug("No client record found on '{bkt}', cas = {cas}", this, cas);
                        pathnotFoundCas = cas;
                        throw new LostCleanupFailedException("No existing Client Record.") { CausingErrorClass = ErrorClass.FailDocNotFound };
                    }

                    clientRecordDetails = new ClientRecordDetails(clientRecord, parsedHlc, ClientUuid, _cleanupWindow);
                    _logger.LogTrace("Found client record for '{bkt}':\n{clientRecordDetails}\n{clientRecord}", FullBucketName, clientRecordDetails, System.Text.Json.JsonSerializer.Serialize(clientRecord, Transactions.MetadataJsonOptions));
                    break;
                }
                catch (Exception ex)
                {
                    var (handled, repeatAfterGetRecord) = await HandleGetRecordFailure(ex, pathnotFoundCas).CAF();
                    repeat = repeatAfterGetRecord;

                    if (!handled)
                    {
                        throw;
                    }
                }
            }
            while (repeat);

            if (clientRecordDetails == null)
            {
                throw new InvalidOperationException(nameof(clientRecordDetails) + " should have been assigned by this point.");
            }
            // NOTE: The RFC says to retry with an exponential backoff, but neither the java implementation nor the FIT tests agree with that.
            await TestHooks.BeforeUpdateRecord(ClientUuid).CAF();
            await _repository.UpdateClientRecord(ClientUuid, _cleanupWindow, ActiveTransactionRecords.AtrIds.NumAtrs, clientRecordDetails.ExpiredClientIds, _cancelToken.Token).CAF();
            _logger.LogTrace("Successfully updated Client Record Entry for {clientUuid} on {bkt}", ClientUuid, FullBucketName);

            return clientRecordDetails;
        }

        private async Task<(bool handled, bool repeatProcessClient)> HandleGetRecordFailure(Exception ex, ulong? pathNotFoundCas)
        {
            var ec = ex.Classify();
            switch (ec)
            {
                case ErrorClass.FailDocNotFound:
                    try
                    {
                        // Client record needs to be created.
                        await TestHooks.BeforeCreateRecord(ClientUuid).CAF();
                        await _repository.CreatePlaceholderClientRecord(pathNotFoundCas, _cancelToken.Token).CAF();
                        _logger.LogDebug("Created placeholder Client Record for '{bkt}', cas = {cas}", FullBucketName, pathNotFoundCas);

                        // On success, call the processClient algo again.
                        return (handled: true, repeatProcessClient: true);
                    }
                    catch (Exception exCreatePlaceholder)
                    {
                        var ecCreatePlaceholder = exCreatePlaceholder.Classify();
                        switch (ecCreatePlaceholder)
                        {
                            case ErrorClass.FailDocAlreadyExists:
                                // continue as success
                                return (handled: true, repeatProcessClient: false);
                            case ErrorClass.FailCasMismatch:
                                _logger.LogWarning("Should not have hit CasMismatch for case FailDocNotFound when creating placeholder client record for {bkt}", FullBucketName);
                                throw;
                            // TODO: Else if BF-CBD-3794, and err indicates a NO_ACCESS
                            default:
                                throw;
                        }
                    }
                // TODO: Handle NoAccess BF-CBD-3794,
                default:
                    // Any other error, propagate it.
                    return (handled: false, repeatProcessClient: false);
            }
        }

        private async Task CleanupAtr(string atrId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, AtrEntry> attempts;
            ParsedHLC? parsedHlc;
            _logger.LogTrace("{clientUUID} Attempting to cleanup {atrId}",ClientUuid, atrId);
            try
            {
                await TestHooks.BeforeAtrGet(atrId).CAF();
                (attempts, parsedHlc) = await _repository.LookupAttempts(atrId, cancellationToken).CAF();
            }
            catch (AuthenticationFailureException)
            {
                // BF-CBD-3794
                throw;
            }
            catch (Exception ex)
            {
                var ec = ex.Classify();
                switch (ec)
                {
                    case ErrorClass.FailDocNotFound:
                    case ErrorClass.FailPathNotFound:
                        // If the ATR is not present, continue as success.
                        _logger.LogTrace("{clientUUID} ATR {atrId} not present on {collection}: {ec}", ClientUuid, atrId,_repository.Keyspace, ec);
                        return;
                    default:
                        // Else if there’s an error, continue as success.
                        _logger.LogWarning("{clientUUID} Failed to look up attempts on ATR {atrId}: {ex}", ClientUuid, atrId, ex);
                        return;
                }
            }

            foreach (var kvp in attempts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Exiting cleanup of attempt {attempt} on {bkt} early due to cancellation.", kvp.Key, FullBucketName);
                    return;
                }

                var (attemptId, attempt) = (kvp.Key, kvp.Value);
                if (attempt == null)
                {
                    continue;
                }

                var isExpired = attempt is { TimestampStartMsecs: not null, ExpiresAfterMsecs: not null }
                                && parsedHlc != null
                                && attempt.TimestampStartMsecs!.Value.AddMilliseconds(attempt.ExpiresAfterMsecs!.Value) < parsedHlc.NowTime;
                if (!isExpired) continue;
                var atrCollection = await AtrRepository.GetAtrCollection(new AtrRef()
                {
                    BucketName = BucketName,
                    ScopeName = ScopeName,
                    CollectionName = CollectionName,
                    Id = atrId
                }, await _repository.GetCollectionAsync().CAF()).CAF();

                if (atrCollection == null)
                {
                    continue;
                }

                var cleanupRequest = new CleanupRequest(
                    AttemptId: attemptId,
                    AtrId: atrId,
                    AtrCollection: atrCollection,
                    InsertedIds: attempt.InsertedIds.ToList(),
                    RemovedIds: attempt.RemovedIds.ToList(),
                    ReplacedIds: attempt.ReplacedIds.ToList(),
                    State: attempt.State,
                    WhenReadyToBeProcessed: DateTimeOffset.UtcNow,
                    ProcessingErrors: new ConcurrentQueue<Exception>(),
                    ForwardCompatibility: kvp.Value.ForwardCompatibility);

                if (_cancelToken.IsCancellationRequested)
                {
                    return;
                }

                await _cleaner.ProcessCleanupRequest(cleanupRequest, isRegular: false).CAF();
            }
        }


        private async Task RemoveClient()
        {
            var retryDelay = 1;
            for (int retryCount = 1; retryDelay <= 250; retryCount++)
            {
                retryDelay = (int)Math.Pow(2, retryCount) + _jitter.Next(10);
                try
                {
                    await TestHooks.BeforeRemoveClient(ClientUuid).CAF();
                    // No cancellation token here: this runs from DisposeAsync after _cancelToken is already
                    // cancelled, and we still want the client record removed during shutdown.
                    await _repository.RemoveClient(ClientUuid).CAF();
                    _logger.LogDebug("Removed client {clientUuid} for {bkt}", ClientUuid, FullBucketName);
                    return;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("Cannot continue cleanup after underlying data access has been disposed for {bkt}", FullBucketName);
                    return;
                }
                catch (AuthenticationFailureException)
                {
                    // BF-CBD-3794
                    _logger.LogWarning("Failed to remove client for '{bkt}' due to auth error", FullBucketName);
                    break;
                }
                catch (Exception ex)
                {
                    var ec = ex.Classify();
                    switch (ec)
                    {
                        case ErrorClass.FailDocNotFound:
                        case ErrorClass.FailPathNotFound:
                            // treat as success
                            _logger.LogInformation("{ec} ignored during Remove Lost Transaction Client.", ec);
                            return;
                        default:
                            _logger.LogWarning("{ec} during Remove Lost Transaction Client, retryCount = {rc}, err = {err}", ec, retryCount, ex.Message);
                            _logger.LogDebug("err = {err}", ex);
                            await Task.Delay(retryDelay).CAF();
                            break;
                    }
                }
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
