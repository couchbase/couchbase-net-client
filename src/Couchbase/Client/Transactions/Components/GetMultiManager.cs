#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.External;
using Couchbase.Client.Transactions.Forwards;
using Couchbase.Client.Transactions.Support;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using TimeoutException = System.TimeoutException;

namespace Couchbase.Client.Transactions.Components;

internal enum Signal
{
    Continue,
    Retry,
    Completed,
    BoundsExceeded,
    ResetAndRetry
}

// This class manages the stages of a GetMulti call.   We have a couple distinct
// phases: FetchingDocuments and DocumentDisambiguation.   This is too much to
// sprinkle into the AttemptContext (one would argue we've already done too much of that).
// So, we encapsulate it here.
internal class GetMultiManager<TSpec, TResult>
    where TSpec : TransactionGetMultiSpecBase
    where TResult : TransactionGetMultiResultBase
{
    private readonly TaskLimiter _taskLimiter;
    private readonly TResult _result;
    private DateTimeOffset _deadline;
    private readonly TimeSpan _kvTimeout;
    private readonly List<TSpec> _specs;
    private readonly TransactionGetMultiMode _mode;
    private readonly bool _allowReplica;
    private readonly AttemptContext _attemptContext;
    private Phase _phase;
    private const int Concurrency = 100;
    private readonly ILogger<GetMultiManager<TSpec, TResult>> _logger;
    private readonly IRedactor _redactor;

    private enum Phase
    {
        FirstDocFetch,
        SubsequentToFirstDocFetch,
        DiscoveredDocsInT1,
        ResolvingT1AtrEntryMissing
    }


    public GetMultiManager(AttemptContext ctx, ILoggerFactory loggerFactory, TimeSpan? kvTimeout,
        List<TSpec> specs, TransactionGetMultiOptionsBase options)
    {
        _specs = specs;
        _mode = options.Mode;
        _allowReplica = typeof(TSpec) ==
                        typeof(TransactionGetMultiReplicaFromPreferredServerGroupSpec);
        _kvTimeout = kvTimeout ?? TimeSpan.FromSeconds(2.5);
        _attemptContext = ctx;
        _logger = loggerFactory.CreateLogger<GetMultiManager<TSpec, TResult>>();
        _taskLimiter = new(Concurrency);
        _deadline = DateTimeOffset.UtcNow + _kvTimeout;
        _phase = Phase.FirstDocFetch;
        _redactor = ctx.Redactor;

        _result = CreateResult(specs);
    }

    private void Log(LogLevel level, string msg, params object?[] args)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["Phase"] = _phase,
                   ["deadline_remaining"] = _deadline - DateTimeOffset.UtcNow
               }))
        {
            _logger.Log(level, msg, args);
        }
    }

    private void LogError(string message, params object?[] args) => Log(LogLevel.Error, message, args);
    private void LogWarning(string message, params object?[] args) => Log(LogLevel.Warning, message, args);
    private void LogInfo(string message, params object?[] args) => Log(LogLevel.Information, message, args);
    private void LogDebug(string message, params object?[] args) => Log(LogLevel.Debug, message, args);

    private static TResult CreateResult(List<TSpec> specs)
    {
        // we just want to create the appropriate result, given the type of spec.   Note that
        // this checks to be sure we declared this with the matching spec and result types as
        // well.
        return (specs, typeof(TResult)) switch
        {
            (List<TransactionGetMultiSpec> s, var t) when t == typeof(TransactionGetMultiResult) =>
                (TResult)(object)new TransactionGetMultiResult(s.Count),
            (List<TransactionGetMultiReplicaFromPreferredServerGroupSpec> s, var t) when t ==
                typeof(TransactionGetMultiReplicaFromPreferredServerGroupResult) =>
                (TResult)(object)new TransactionGetMultiReplicaFromPreferredServerGroupResult(
                    s.Count),
            _ => throw new InvalidArgumentException("Unexpected spec/result types.")
        };
    }

    public async Task<TResult> RunAsync()
    {
        var msDelay = 1;
        while (true)
        {
            _attemptContext.CheckExpiryAndThrow(null, "getMulti");


            var signal = await FetchDocuments().CAF();
            switch (signal)
            {
                case Signal.Completed:
                case Signal.Continue:
                    return _result;
                case Signal.ResetAndRetry:
                    if (_phase != Phase.FirstDocFetch)
                        _phase = Phase.SubsequentToFirstDocFetch;
                    _result.ResetResults();
                    break;
                case Signal.BoundsExceeded:
                    if (_result.AllFetched())
                        return _result;
                    LogWarning("Received bounds exceeded, raising error");
                    throw ErrorBuilder.CreateError(_attemptContext, ErrorClass.FailOther)
                        .RetryTransaction().Build();
                case Signal.Retry:
                    break;
                default:
                    LogWarning("Unexpected signal {signal}", signal);
                    throw ErrorBuilder.CreateError(_attemptContext, ErrorClass.FailOther)
                        .RetryTransaction().Build();
            }

            await Task.Delay(msDelay).CAF();
            msDelay = msDelay * 2;
        }
    }

    private async Task<Signal> Disambiguate()
    {
        // get the txn count ignoring _this_ transaction.
        var txns =
            _result.CountUniqueTransactionsInResults(_attemptContext._overallContext.TransactionId);
        return txns switch
        {
            0 => Signal.Continue,
            1 => await ReadSkewResolution().CAF(),
            _ => Signal.ResetAndRetry
        };
    }

    private async Task<Signal> ReadSkewResolution()
    {
        try
        {
            // get the first AtrRef that isn't associated with our transaction.
            var (atrRef, id)= _result.GetFirstAtrRef(_attemptContext._overallContext.TransactionId);
            if (id.AttemptId == null)
            {
                throw new InvalidArgumentException("Attempt identifier is invalid");
            }
            LogDebug("Read skew resolution reading atr for {atrRef}", atrRef.Id);
            var atrCollection =
                await AtrRepository.GetAtrCollection(atrRef, _specs[0].Collection).CAF(); // TODO: adjust timeout!!
            // Need to add error handling and timeout passed into the FindEntryForTransacton call
            var atr = await AtrRepository.FindEntryForTransaction(atrCollection!, atrRef.Id!, id.AttemptId).CAF();
            if (atr == null)
            {
                // the txn could have expired pre-commit and been cleaned up, or it could have been
                // actually committed then cleaned up.  So... we have to disambiguate these cases.
                // To do so, we re-fetch the docs in this txn...
                LogDebug("Atr {atrRef} could not be found, refetching documets", atrRef.Id);
                if (_phase == Phase.ResolvingT1AtrEntryMissing)
                {
                    var res = _result.FindFirstResult(result =>
                        (result != null && result.TransactionXattrs != null &&
                         result.TransactionXattrs?.Id?.Transactionid !=
                         _attemptContext._overallContext.TransactionId));

                    return res != null
                        ? Signal.Completed
                        : Signal.ResetAndRetry;
                }
                _phase = Phase.ResolvingT1AtrEntryMissing;
                return Signal.Retry;
            }

            await ForwardCompatibility
                .Check(_attemptContext, ForwardCompatibility.GetsReadingAtr,
                    atr.ForwardCompatibility).CAF();

            switch (atr.State)
            {
                case AttemptStates.ABORTED:
                case AttemptStates.PENDING:
                    return Signal.Completed;
                case AttemptStates.COMMITTED:
                    if (_phase == Phase.DiscoveredDocsInT1)
                    {
                        LogDebug(
                            "Attempt committed, phase = {phase}, marking to use postCommit results",
                            _phase);
                        _result.UsePostCommit();
                        return Signal.Completed;
                    }
                    LogDebug("Atr says {atrRef} has committed, so checking fetched documents", atrRef.Id!);

                    var allDocs = atr.AllDocRecords.Select(r => r.Id);
                    List<int> docsToFetch = [];

                    //  let's see if any fetched documents with no transaction xattrs actually
                    //  are part of this transaction.   Presumably we fetched, then this txn
                    // happened, so we are skewed.
                    _result.IterateResults(
                        result => result is { TransactionXattrs: null } &&
                                  allDocs.Contains(result.Id), (_, idx) => docsToFetch.Add(idx));
                    if (docsToFetch.Count == 0)
                    {
                        _result.UsePostCommit();
                        return Signal.Completed;
                    }

                    // refetch docs in T1 which had no T1 metadata...
                    await RefetchDocuments(docsToFetch).CAF();

                    // Now we need to mark the docs, update the phase and retry...
                    LogDebug(
                        "Found docs without xattrs but in transaction, marking and retrying...");
                    _phase = Phase.DiscoveredDocsInT1;
                    foreach (var idx in docsToFetch)
                        _result.GetMultiSpecResult(idx).State =
                            GetMultiSpecResult.DocState.WereInT1;
                    return Signal.Retry;
                default:
                    LogWarning("Unexpected state from FetchDocuments: {}", atr.State);
                    return Signal.ResetAndRetry;
            }
        }
        catch (TimeoutException)
        {
            return _mode == TransactionGetMultiMode.PrioritizeReadSkewDetection
                ? Signal.Retry
                : Signal.BoundsExceeded;
        }
        catch (TransactionOperationFailedException ex)
        {
            // pass the TransactionOperationFailedExceptions up the chain unchanged.
            LogDebug("got {ex}, propogating it upwards...", ex);
            throw;
        }
        catch (Exception ex)
        {
            var ec = ex.Classify();
            LogWarning("Exception '{ex}' during ReadSkewResolution", ex.Message);
            throw ErrorBuilder.CreateError(_attemptContext, ec, ex).RetryTransaction().Build();
        }
    }

    private async Task RefetchDocuments(List<int>? docsIdxToFetch = null)
    {
        // we only fetch those that are in a txn other than this one, unless passed an explicit
        // list of doc indexes
        if (docsIdxToFetch == null)
        {
            docsIdxToFetch = new List<int>();
            _result.IterateResults(result => (result != null && result.TransactionXattrs != null &&
                                              result.TransactionXattrs?.Id?.Transactionid !=
                                              _attemptContext._overallContext.TransactionId),
                (_, originalIndex) => docsIdxToFetch.Add(originalIndex));
        }
        LogDebug("refetching docs {docs}", docsIdxToFetch);
        // now use taskLimiter again to refetch...
        foreach (var idx in docsIdxToFetch)
        {
            var tuple = (spec: _specs[idx], idx);
            var kvTimeoutInFuture = DateTimeOffset.UtcNow + _kvTimeout;
            var deadline = _deadline < kvTimeoutInFuture ? _deadline : kvTimeoutInFuture;
            _taskLimiter.Run(tuple,
                async t => await FetchDocument(t.spec.Collection, t.spec.Id, deadline,
                    t.spec.Transcoder, t.idx).CAF());
        }

        await _taskLimiter.WaitAllAsync().CAF();

    }

    private async Task<Signal> FetchDocuments()
    {
        for (var idx = 0; idx < _specs.Count; idx++)
        {
            if (!_result.ShouldFetch(idx)) continue;

            var tuple = (spec: _specs[idx], idx);
            _taskLimiter.Run(tuple,
                async t => await FetchDocument(t.spec.Collection, t.spec.Id, _deadline,
                    t.spec.Transcoder, t.idx).CAF());
        }

        await _taskLimiter.WaitAllAsync().CAF();

        // if any signals, return first one we find
        if (_result.GetFirstSignal() != null)
        {
            return _result.GetFirstSignal()!.Value;
        }

        switch (_mode)
        {
            case TransactionGetMultiMode.DisableReadSkewDetection:
                break;
            case TransactionGetMultiMode.PrioritizeLatency:
                _deadline = DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(100);
                break;
            case TransactionGetMultiMode.PrioritizeReadSkewDetection:
                _deadline = _attemptContext._overallContext.AbsoluteExpiration;
                break;
            default:
               LogWarning("Unexpected TransactionGetMultiMode: {mode}", _mode);
                break;
        }

        if (_phase == Phase.FirstDocFetch)
        {
            _phase = Phase.SubsequentToFirstDocFetch;
        }
        return await Disambiguate().CAF();
    }

    private async Task FetchDocument(ICouchbaseCollection collection, string id,
        DateTimeOffset deadline,
        ITypeTranscoder? transcoder, int specIndex)
    {
        await TaskRepeater.RepeatUntilSuccessOrThrow(async () =>
        {
            try
            {
                _attemptContext.CheckExpiryAndThrow(null, "getMultiIndividualDocument");
                LogDebug(
                    $"Fetching document {id}, allowReplica={_allowReplica}",
                    _redactor.UserData(id), _allowReplica);
                var result =
                    await _attemptContext._docs
                        .LookupDocumentAsync(collection, id, deadline, transcoder,
                            _allowReplica)
                        .CAF();

                // Get and GetMulti ForwardCompat check if there is transactional metadata...
                if (result?.TransactionXattrs != null) {
                    await ForwardCompatibility.Check(_attemptContext, ForwardCompatibility.GetMulti,
                        result.TransactionXattrs?.ForwardCompatibility).CAF();
                    await ForwardCompatibility.Check(_attemptContext, ForwardCompatibility.Gets,
                        result.TransactionXattrs?.ForwardCompatibility).CAF();

                }

                // we have a doc, cram it in the results...
                _result.InsertResult(result, specIndex);
                LogDebug("Found document {id}, inserting in GetMulti result", _redactor.UserData(id));
                return RepeatAction.NoRepeat;

            }
            catch (DocumentUnretrievableException)
            {
                LogDebug("Document {idx} not retrievable", _redactor.UserData(id));
                _result.InsertResult(null, specIndex);
                return RepeatAction.NoRepeat;
            }
            catch (DocumentNotFoundException)
            {
                // just make the result null, no problems
                LogDebug("Document {id} not found, inserting in GetMulti result", _redactor.UserData(id));
                _result.InsertResult(null, specIndex);
                return RepeatAction.NoRepeat;
            }
            catch (Couchbase.Core.Exceptions.TimeoutException e)
            {
                if (_mode == TransactionGetMultiMode.PrioritizeReadSkewDetection)
                {
                    LogDebug("{e}, retrying FetchDocument for {id}", e, _redactor.UserData(id));
                    return RepeatAction.RepeatWithBackoff;
                }

                // this means our result should be just a signal
                LogDebug("{e} - setting signal to BoundsExceeded for document {id}", e, _redactor.UserData(id));
                _result.InsertSignal(Signal.BoundsExceeded, specIndex);
                return RepeatAction.NoRepeat;
            }
            catch (TransactionOperationFailedException)
            {
                throw;
            }
            catch (Exception e)
            {
                var ec = e.Classify();
                LogDebug("FetchDocument got exception {e}, {ec} for doc {id}", e, ec, _redactor.UserData(id));
                if (ec == ErrorClass.FailTransient)
                {
                    return RepeatAction.RepeatWithBackoff;
                }

                // otherwise, we need to raise a real error...
                var err = ErrorBuilder.CreateError(_attemptContext, ec, e);
                // rollback unless ec==FAIL_HARD
                if (ec == ErrorClass.FailHard) err.DoNotRollbackAttempt();
                throw err.Build();
            }
        }).CAF();
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
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
