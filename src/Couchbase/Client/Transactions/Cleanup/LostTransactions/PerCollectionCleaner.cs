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
        private readonly Timer _processCleanupTimer;
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


        public ICleanupTestHooks TestHooks { get; set; } = DefaultCleanupTestHooks.Instance;
        public long RunCount => Interlocked.Read(ref _runCount);
        private bool Running => !_cancelToken.IsCancellationRequested;

        public PerCollectionCleaner(string clientUuid, Cleaner cleaner, CleanerRepositoryBase repository,TimeSpan cleanupWindow, ILoggerFactory loggerFactory, bool startDisabled = false, Action<Keyspace>? onCollectionNotFound = null, TimeProvider? timeProvider = null)
        {
            ClientUuid = clientUuid;
            _cleaner = cleaner; // TODO: Cleaner should have its data access refactored into CleanerRepositoryBase, and then that should be made a property, eliminating the need for a _repository variable here.
            _repository = repository;
            _cleanupWindow = cleanupWindow;
            _onCollectionNotFound = onCollectionNotFound;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _atrsToClean = new AtrCleanupQueue(_timeProvider);
            _logger = loggerFactory.CreateLogger<PerCollectionCleaner>();
            _processCleanupTimer = new Timer(
                callback: TimerCallback,
                state: null,
                dueTime: startDisabled ? -1 : 0,
                period: (int)cleanupWindow.TotalMilliseconds);

            FullBucketName = (bucket: BucketName, scope: ScopeName, collection: CollectionName, clientUuid: ClientUuid).ToString();
            _logger.LogInformation("Started PerCollectionCleaner on '{coll}'", repository.Keyspace);
        }

        public void Start()
        {
            _processCleanupTimer.Change(TimeSpan.Zero, _cleanupWindow);
        }

        public void Stop()
        {
            _processCleanupTimer.Change(-1, -1);
            _cancelToken.Cancel();
            _logger.LogDebug($"Cancelling per collection cleaner for '{ClientUuid}'");
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

        public void Dispose()
        {
            if (!_cancelToken.IsCancellationRequested)
            {
                Stop();
            }
            else
            {
                _logger.LogDebug("(already disposed)");
            }
        }


        public async ValueTask DisposeAsync()
        {
            if (!_cancelToken.IsCancellationRequested)
            {
                _logger.LogDebug("Disposing of PerCollectionCleaner for {bkt}", FullBucketName);
                Dispose();
                // at this point, there will be no more timer callbacks triggered, so lets
                // wait for the mutex, at which point the current ProcessClient (if any) is
                // done (and there will be no more).
                _logger.LogDebug("waiting for cleanup Task on {bkt}", FullBucketName);
                await  _timerCallbackMutex.WaitAsync().CAF();
                _logger.LogDebug("cleanup Task stopped for {bkt}", FullBucketName);
                _processCleanupTimer.Dispose();
                _logger.LogDebug("cleanup Timer stopped for {bkt}", FullBucketName);
                await RemoveClient().CAF();
                _logger.LogDebug("removed ClientRecord for {bkt}", FullBucketName);
                _cancelToken.Dispose();
            }
            else
            {
                _logger.LogDebug("PerCollectionCleaner for '{bkt}' is already disposed.", FullBucketName);
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
                _logger.LogDebug("Ignoring error releasing timer callback mutex on {bkt}: {ex}", FullBucketName, ex);
            }
        }

        private async void TimerCallback(object? state)
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

            try
            {
                _ = await ProcessClient().CAF();
                TryReleaseMutex();
            }
            catch (AuthenticationFailureException)
            {
                // BF-CBD-3794
                _logger.LogDebug("Exiting cleanup of '{bkt}' due to access error", FullBucketName);
                // must release the mutex before disposing (because we acquire it in DisposeAsync)
                TryReleaseMutex();
                await DisposeAsync().CAF();
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown: the cancellation token tripped mid-cycle (a pending
                // Task.Delay or an early ThrowIfCancellationRequested in CleanupAtr). Exit quietly.
                _logger.LogDebug("Cleanup of '{bkt}' cancelled during shutdown.", FullBucketName);
                TryReleaseMutex();
            }
            catch (Exception ex) when (IsCollectionNotFound(ex))
            {
                // The server doesn't recognize this collection (deleted, or a misconfigured keyspace
                // that never existed) - stop cleaning it.
                _logger.LogWarning("Stopping lost cleanup of '{bkt}': collection not found (deleted or misconfigured).", FullBucketName);
                // release the mutex before DisposeAsync, which also acquires it.
                TryReleaseMutex();
                _onCollectionNotFound?.Invoke(_repository.Keyspace);
                await DisposeAsync().CAF();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Processing of bucket '{bkt}' failed unexpectedly: {ex}", FullBucketName, ex);
                TryReleaseMutex();
            }
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
