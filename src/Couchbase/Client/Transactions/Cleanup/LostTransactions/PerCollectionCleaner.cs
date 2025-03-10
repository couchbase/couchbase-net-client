#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ICleanerRepository _repository;
        private readonly TimeSpan _cleanupWindow;
        private readonly ILogger<PerCollectionCleaner> _logger;
        private readonly Timer _processCleanupTimer;
        private readonly Random _jitter = new Random();
        private readonly SemaphoreSlim _timerCallbackMutex = new (1);
        private long _runCount;
        private readonly object _atrsToCleanLock = new();
        private ConcurrentBag<string> _atrsToClean = new ConcurrentBag<string>();
        private readonly CancellationTokenSource _cancelToken = new ();


        public ICleanupTestHooks TestHooks { get; set; } = DefaultCleanupTestHooks.Instance;
        public long RunCount => Interlocked.Read(ref _runCount);
        private bool Running => !_cancelToken.IsCancellationRequested;

        public PerCollectionCleaner(string clientUuid, Cleaner cleaner, ICleanerRepository repository,TimeSpan cleanupWindow, ILoggerFactory loggerFactory, bool startDisabled = false)
        {
            ClientUuid = clientUuid;
            _cleaner = cleaner; // TODO: Cleaner should have its data access refactored into ICleanerRepository, and then that should be made a property, eliminating the need for a _repository variable here.
            _repository = repository;
            _cleanupWindow = cleanupWindow;
            _logger = loggerFactory.CreateLogger<PerCollectionCleaner>();
            _processCleanupTimer = new Timer(
                callback: TimerCallback,
                state: null,
                dueTime: startDisabled ? -1 : 0,
                period: (int)cleanupWindow.TotalMilliseconds);

            FullBucketName = (bucket: BucketName, scope: ScopeName, collection: CollectionName, clientUuid: ClientUuid).ToString();
            _logger.LogInformation("Started PerCollectionCleaner on '{coll}'", new Keyspace(repository.Collection));
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
                _timerCallbackMutex.Release();
            }
            catch (AuthenticationFailureException)
            {
                // BF-CBD-3794
                _logger.LogDebug("Exiting cleanup of '{bkt}' due to access error", FullBucketName);
                // must release the mutex before disposing (because we acquire it in DisposeAsync)
                _timerCallbackMutex.Release();
                await DisposeAsync().CAF();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Processing of bucket '{bkt}' failed unexpectedly: {ex}", FullBucketName, ex);
                _timerCallbackMutex.Release();
            }
        }

        // method referred to as "Per Bucket Algo" in the RFC
        internal async Task<ClientRecordDetails> ProcessClient(bool cleanupAtrs = true)
        {
            _logger.LogDebug("Looking for lost transactions on bucket '{bkt}'", FullBucketName);
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

            var sw = Stopwatch.StartNew();

            // for fit tests, we may want to manipulate the client records without actually bothering with cleanup.
            if (cleanupAtrs)
            {
                // we may not have enough time to process every ATR in the configured window.  Process a random member.
                // heuristic: fill a bag with random members, process each until the bag is empty, then re-fill the bag.
                //            This avoids the pathological case where randomization means ATRs get skipped and never/seldom processed.
                long cleanedThisCycle = 0;
                long atrsHandledByThisClient = ActiveTransactionRecords.AtrIds.NumAtrs;
                while (cleanedThisCycle < ActiveTransactionRecords.AtrIds.NumAtrs)
                {
                    var checkAtrLimitWatch = Stopwatch.StartNew();
                    if (!_atrsToClean.TryTake(out var atrId))
                    {
                        lock (_atrsToCleanLock)
                        {
                            _atrsToClean = new ConcurrentBag<string>(clientRecordDetails.AtrsHandledByThisClient);
                            if (!_atrsToClean.TryTake(out atrId))
                            {
                                _logger.LogWarning("No ATRs handled by this client?");
                                break;
                            }

                            _logger.LogDebug("Refilled bag with {totalAtrs} ATRids to process for on {bkt}", atrsHandledByThisClient, FullBucketName);
                        }
                    }

                    if (_cancelToken.IsCancellationRequested)
                    {
                        sw.Stop();
                        _logger.LogDebug("Exiting cleanup of ATR {atr} on {bkt} early due to cancellation after {elapsedMs}ms and {cleanedThisCycle}/{totalAtrs} processed.", atrId, FullBucketName, sw.Elapsed.TotalMilliseconds, cleanedThisCycle, atrsHandledByThisClient);
                        break;
                    }

                    // Every checkAtrEveryNMillis, handle an ATR with id atrId
                    await CleanupAtr(atrId, _cancelToken.Token).CAF();
                    Interlocked.Increment(ref _runCount);
                    Interlocked.Increment(ref cleanedThisCycle);
                    sw.Stop();
                    _logger.LogDebug($"Cleanup of ATR {atrId} complete in {sw.Elapsed.TotalMilliseconds}ms");
                    var necessaryDelay = (int)Math.Max(0, Math.Min(_cleanupWindow.TotalMilliseconds, (clientRecordDetails.CheckAtrTimeWindow - checkAtrLimitWatch.Elapsed).TotalMilliseconds));
                    if (_cancelToken.IsCancellationRequested) break;
                    // under normal circumstances, the cleanup window will be 60 seconds, the delay will be significant,
                    // and Task.Delay is appropriate and efficient
                    if (necessaryDelay >= 10)
                    {
                        await Task.Delay(necessaryDelay).CAF();
                    }
                    // if the user has specified a short cleanup window (most likely tests), then the delay will be short
                    // and Task.Delay's non-guaranteed behavior will result in extra delay and be too slow to maintain rhythm.
                    else if (necessaryDelay >= 1)
                    {
                        SpinWait.SpinUntil(() => checkAtrLimitWatch.Elapsed > clientRecordDetails.CheckAtrTimeWindow, _cleanupWindow);
                    }
                }
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
                    (ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas) = await _repository.GetClientRecord().CAF();
                    if (clientRecord == null)
                    {
                        _logger.LogDebug("No client record found on '{bkt}', cas = {cas}", this, cas);
                        pathnotFoundCas = cas;
                        throw new LostCleanupFailedException("No existing Client Record.") { CausingErrorClass = ErrorClass.FailDocNotFound };
                    }

                    clientRecordDetails = new ClientRecordDetails(clientRecord, parsedHlc, ClientUuid, _cleanupWindow);
                    _logger.LogDebug("Found client record for '{bkt}':\n{clientRecordDetails}\n{clientRecord}", FullBucketName, clientRecordDetails, Newtonsoft.Json.Linq.JObject.FromObject(clientRecord).ToString());
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
            await _repository.UpdateClientRecord(ClientUuid, _cleanupWindow, ActiveTransactionRecords.AtrIds.NumAtrs, clientRecordDetails.ExpiredClientIds).CAF();
            _logger.LogDebug("Successfully updated Client Record Entry for {clientUuid} on {bkt}", ClientUuid, FullBucketName);

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
                        await _repository.CreatePlaceholderClientRecord(pathNotFoundCas).CAF();
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
            Dictionary<string, AtrEntry> attempts;
            ParsedHLC? parsedHlc;
            _logger.LogDebug("{clientUUID} Attempting to cleanup {atrId}",ClientUuid, atrId);
            try
            {
                await TestHooks.BeforeAtrGet(atrId).CAF();
                (attempts, parsedHlc) = await _repository.LookupAttempts(atrId).CAF();
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
                        _logger.LogTrace("{clientUUID} ATR {atrId} not present on {collection}: {ec}", ClientUuid, atrId,_repository.Collection.MakeKeyspace(), ec);
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
                }, _repository.Collection).CAF();

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
