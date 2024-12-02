#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Integrated.Transactions.Components;
using Couchbase.Integrated.Transactions.DataAccess;
using Couchbase.Integrated.Transactions.DataModel;
using Couchbase.Integrated.Transactions.Error;
using Couchbase.Integrated.Transactions.Error.Internal;
using Couchbase.Integrated.Transactions.Internal.Test;
using Microsoft.Extensions.Logging;

namespace Couchbase.Integrated.Transactions.Cleanup.LostTransactions
{
    internal class PerCollectionCleaner : IAsyncDisposable
    {
        public string ClientUuid { get; }

        private readonly Cleaner _cleaner;
        private readonly CleanerRepository _repository;
        private readonly TimeSpan _cleanupWindow;
        private readonly CancellationTokenSource _shutdownToken;
        private readonly ILogger<PerCollectionCleaner> _logger;
        private readonly Timer _processCleanupTimer;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Random _jitter = new Random();
        private readonly SemaphoreSlim _timerCallbackMutex = new SemaphoreSlim(1);
        private long _runCount = 0;
        private readonly object _atrsToCleanLock = new();
        private ConcurrentBag<string> _atrsToClean = new ConcurrentBag<string>();

        public TestHookMap TestHooks { get; set; } = new();
        public long RunCount => Interlocked.Read(ref _runCount);
        public bool Running => !_cts.IsCancellationRequested;

        public PerCollectionCleaner(string clientUuid, Cleaner cleaner, CleanerRepository repository,TimeSpan cleanupWindow, ILoggerFactory loggerFactory, CancellationToken shutdownToken, bool startDisabled = false)
        {
            ClientUuid = clientUuid;
            _cleaner = cleaner;
            _repository = repository;
            _cleanupWindow = cleanupWindow;
            _logger = loggerFactory.CreateLogger<PerCollectionCleaner>();
            _processCleanupTimer = new System.Threading.Timer(
                callback: TimerCallback,
                state: null,
                dueTime: startDisabled ? -1 : 0,
                period: (int)cleanupWindow.TotalMilliseconds);

            FullClientName = $"{KeySpace}, {ClientUuid}";
            _shutdownToken = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, _cts.Token);
        }

        public void Start()
        {
            _processCleanupTimer.Change(TimeSpan.Zero, _cleanupWindow);
        }

        public void Stop()
        {
            _processCleanupTimer.Change(-1, (int)_cleanupWindow.TotalMilliseconds);
        }

        public KeySpace KeySpace => _repository.KeySpace;

        public string FullClientName { get; }

        public override string ToString()
        {
            return new Summary(FullClientName, ClientUuid, Running, RunCount).ToString();
        }

        private record Summary(string FullBucketName, string ClientUuid, bool Running, long RunCount);

        public void Dispose()
        {
            if (!_cts.IsCancellationRequested)
            {
                Stop();
                _processCleanupTimer.Change(-1, -1);
                _processCleanupTimer.Dispose();
                _cts.Cancel();
            }
            else
            {
                _logger.LogDebug("PerCollectionCleaner:{clientId} (already disposed)", ClientUuid);
            }
        }


        public async ValueTask DisposeAsync()
        {
            if (!_cts.IsCancellationRequested)
            {
                _logger.LogDebug("Disposing {bkt}", FullClientName);
                await RemoveClient().CAF();
                Dispose();
                _timerCallbackMutex.Release();
            }
            else
            {
                _logger.LogDebug("PerCollectionCleaner for '{fullClientName}' is already disposed.", FullClientName);
            }
        }

        private async void TimerCallback(object? state)
        {
            if (_shutdownToken.Token.IsCancellationRequested)
            {
                Stop();
                _logger.LogDebug("TimerCallback after already disposed.");
                await RemoveClient().CAF();
                return;
            }

            var enteredWithoutTimeout = await _timerCallbackMutex.WaitAsync(_cleanupWindow).CAF();
            if (!enteredWithoutTimeout)
            {
                _logger.LogDebug("Timed out while waiting for overlapping callbacks on {fullClientName}", FullClientName);
                return;
            }

            if (_shutdownToken.Token.IsCancellationRequested)
            {
                _logger.LogDebug("TimerCallback cancelled while waiting.");
                return;
            }

            try
            {
                _ = await ProcessClient().CAF();
            }
            catch (AuthenticationFailureException)
            {
                // BF-CBD-3794
                _logger.LogWarning("Exiting cleanup of '{clientName}' due to access error", FullClientName);
                await DisposeAsync().CAF();
            }
            catch (ObjectDisposedException ode)
            {
                _logger.LogCritical("Object {disposedName} Disposed, but cts.IsCancellationRequested = {isCancelled}",
                    ode.ObjectName,
                    _cts.IsCancellationRequested);
                _cts.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Processing of bucket '{fullClientName}' failed unexpectedly: {ex}", FullClientName, ex);
            }
            finally
            {
                _timerCallbackMutex.Release();
            }
        }

        // method referred to as "Per Bucket Algo" in the RFC
        internal async Task<ClientRecordDetails> ProcessClient(bool cleanupAtrs = true)
        {
            var swProcessClient = Stopwatch.StartNew();
            _logger.LogDebug("Looking for lost transactions on bucket '{clientName}'", FullClientName);

            ClientRecordDetails clientRecordDetails = await EnsureClientRecordIsUpToDate().CAF();
            if (clientRecordDetails.OverrideActive)
            {
                _logger.LogInformation("Cleanup of '{fullClientName}' is currently disabled by another actor.", FullClientName);
                return clientRecordDetails;
            }

            var elapsed1 = swProcessClient.Elapsed.TotalMilliseconds;

            var sw = Stopwatch.StartNew();
            using var boundedCleanup = new CancellationTokenSource(_cleanupWindow);
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(boundedCleanup.Token, _cts.Token);

            // for fit tests, we may want to manipulate the client records without actually bothering with cleanup.
            if (cleanupAtrs)
            {
                // we may not have enough time to process every ATR in the configured window.  Process a random member.
                // heuristic: fill a bag with random members, process each until the bag is empty, then re-fill the bag.
                //            This avoids the pathological case where randomization means ATRs get skipped and never/seldom processed.
                var numAtrsHandledByThisClient = clientRecordDetails.AtrsHandledByThisClient.Count;
                if (numAtrsHandledByThisClient <= 0)
                {
                    return clientRecordDetails;
                }

                var elapsed2 = swProcessClient.Elapsed.TotalMilliseconds;
                long checkEveryNMillis = (long)(_cleanupWindow.TotalMilliseconds / numAtrsHandledByThisClient);
                if (!_atrsToClean.TryPeek(out var atrId))
                {
                    lock (_atrsToCleanLock)
                    {
                        _atrsToClean = new ConcurrentBag<string>(clientRecordDetails.AtrsHandledByThisClient);
                        if (!_atrsToClean.TryPeek(out atrId))
                        {
                            _logger.LogWarning("No ATRs handled by this client?");
                            return clientRecordDetails;
                        }

                        _logger.LogDebug("Refilled bag with {totalAtrs} ATRids to process for on {clientName}", numAtrsHandledByThisClient, FullClientName);
                    }
                }


#if NET5_0_OR_GREATER
                await Parallel.ForEachAsync(EnumerateAndTake(_atrsToClean, linkedSource.Token), linkedSource.Token, CleanupAtr).ConfigureAwait(false);
#else
                // FIXME:  implement for .NET Standard 2.0
#endif
            }

            return clientRecordDetails;
        }

        private IEnumerable<string> EnumerateAndTake(ConcurrentBag<string> bag, CancellationToken token)
        {
            while (!token.IsCancellationRequested && bag.TryTake(out var s))
            {
                yield return s;
            }
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
                    if (_shutdownToken.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    // Parse the client record.
                    await TestHooks.Async(HookPoint.CleanupBeforeDocGet, null, ClientUuid).CAF();
                    (ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas) = await _repository.GetClientRecord(_cts.Token).CAF();

                    if (clientRecord == null)
                    {
                        _logger.LogDebug("No client record found on '{clientName}', cas = {cas}", this, cas);
                        pathnotFoundCas = cas;
                        throw new LostCleanupFailedException("No existing Client Record.") { CausingErrorClass = ErrorClass.FailDocNotFound };
                    }

                    clientRecordDetails = new ClientRecordDetails(clientRecord, parsedHlc!, ClientUuid, _cleanupWindow);
                    _logger.LogDebug("Found client record for '{clientName}':\n{clientRecordDetails}\n{clientRecord}", FullClientName, clientRecordDetails, Newtonsoft.Json.Linq.JObject.FromObject(clientRecord).ToString());
                    break;
                }
                catch (Exception ex)
                {
                    (var handled, var repeatAfterGetRecord) = await HandleGetRecordFailure(ex, pathnotFoundCas).CAF();
                    repeat = repeatAfterGetRecord;

                    if (!handled)
                    {
                        throw;
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            while (repeat && !_shutdownToken.Token.IsCancellationRequested);

            if (clientRecordDetails == null)
            {
                throw new InvalidOperationException(nameof(clientRecordDetails) + " should have been assigned by this point.");
            }

            // NOTE: The RFC says to retry with an exponential backoff, but neither the java implementation nor the FIT tests agree with that.
            await TestHooks.Async(HookPoint.ClientRecordBeforeUpdate, null, ClientUuid).CAF();
            await _repository.UpdateClientRecord(ClientUuid, _cleanupWindow, ActiveTransactionRecords.AtrIds.NumAtrs, clientRecordDetails.ExpiredClientIds).CAF();
            _logger.LogDebug("Successfully updated Client Record Entry for {clientUuid} on {fullClientName}", ClientUuid, FullClientName);

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
                        await TestHooks.Async(HookPoint.ClientRecordBeforeCreate, null, ClientUuid).CAF();
                        await _repository.CreatePlaceholderClientRecord(pathNotFoundCas).CAF();
                        _logger.LogDebug("Created placeholder Client Record for '{fullClientName}', cas = {cas}", FullClientName, pathNotFoundCas);

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
                                _logger.LogWarning("Should not have hit CasMismatch for case FailDocNotFound when creating placeholder client record for {clientName}", FullClientName);
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

        private async ValueTask CleanupAtr(string atrId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("CleanupATR: Checking ATR {atrId}", atrId);
            Dictionary<string, AtrEntry>? attempts = null;
            ParsedHLC? parsedHlc = null;
            try
            {
                (attempts, parsedHlc, var timingInfo) = await _repository.LookupAttempts(atrId).CAF();
                _logger.LogDebug("lookup attempts timing = {timingInfo}", timingInfo);
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
                        _logger.LogDebug("ATR {atrId} not present on {collection}: {ec}", atrId, _repository.KeySpace,
                            ec);
                        return;
                    default:
                        // Else if there’s an error, continue as success.
                        _logger.LogWarning("Failed to look up attempts on ATR {atrId}: {ex}", atrId, ex);
                        return;
                }
            }
            finally
            {
                _logger.LogDebug("CleanupATR: Checked ATR {atrId}, attemptCount = {attemptCount}", atrId, attempts?.Count);
            }

            if (attempts is not { Count: > 0 })
            {
                return;
            }

            foreach (var kvp in attempts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Exiting cleanup of attempt {attempt} on {fullClientName} early due to cancellation.", kvp.Key, FullClientName);
                    return;
                }

                (var attemptId, var attempt) = (kvp.Key, kvp.Value);
                if (attempt == null)
                {
                    continue;
                }

                var isExpired = attempt?.TimestampStartMsecs.HasValue == true
                    && attempt?.ExpiresAfterMsecs.HasValue == true
                    && attempt.TimestampStartMsecs!.Value.AddMilliseconds(attempt.ExpiresAfterMsecs!.Value) < parsedHlc!.NowTime;
                if (isExpired)
                {
                    var anyCollection = await _repository.GetCollection().CAF();
                    var atrCollection = await AtrRepository.GetAtrCollection(new AtrRef()
                    {
                        BucketName = this.KeySpace.Bucket,
                        ScopeName = this.KeySpace.Scope,
                        CollectionName = this.KeySpace.Collection,
                        Id = atrId
                    }, anyCollection).CAF();

                    if (atrCollection == null)
                    {
                        continue;
                    }

                    var cleanupRequest = new CleanupRequest(
                        AttemptId: attemptId,
                        AtrId: atrId,
                        AtrCollection: atrCollection,
                        InsertedIds: attempt!.InsertedIds.ToList(),
                        RemovedIds: attempt.RemovedIds.ToList(),
                        ReplacedIds: attempt.ReplacedIds.ToList(),
                        State: attempt.State,
                        WhenReadyToBeProcessed: DateTimeOffset.UtcNow,
                        ProcessingErrors: new ConcurrentQueue<Exception>(),
                        ForwardCompatibility: kvp.Value.ForwardCompatibility);

                    if (_cts.IsCancellationRequested)
                    {
                        return;
                    }

                    // FIXME:  Cleanup is going too slow, so not consistently cleaning up what tests check.
                    // FIXME:  Can we dump this in the CleanupWorkQueue instead?
                    await _cleaner.ProcessCleanupRequest(cleanupRequest, isRegular: false).CAF();
                }
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
                    await TestHooks.Async(HookPoint.ClientRecordBeforeRemoveClient, null, ClientUuid).CAF();
                    await _repository.RemoveClient(ClientUuid).CAF();
                    _logger.LogDebug("Removed client {clientUuid} for {clientName}", ClientUuid, FullClientName);
                    return;
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("Cannot continue cleanup after underlying data access has been disposed for {clientName}", FullClientName);
                    return;
                }
                catch (AuthenticationFailureException)
                {
                    // BF-CBD-3794
                    _logger.LogWarning("Failed to remove client for '{clientName}' due to auth error", FullClientName);
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
                            _logger.LogInformation("{ec} ignored during Remove Lost Transaction Client: {clientName}.", ec, FullClientName);
                            return;
                        default:
                            _logger.LogWarning("{ec} during Remove Lost Transaction Client, retryCount = {rc}, err = {err}, client={clientName}", ec, retryCount, ex.Message, FullClientName);
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
 *    @copyright 2024 Couchbase, Inc.
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







