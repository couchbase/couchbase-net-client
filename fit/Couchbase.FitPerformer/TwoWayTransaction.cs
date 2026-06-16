#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Grpc.Protocol.Transactions;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.External;
using ErrorClass = Couchbase.Client.Transactions.Error.ErrorClass;
using Couchbase.Client.Transactions;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Exception = System.Exception;
using Couchbase.FitPerformer.Utils;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.FitPerformer.Workload;
using TransactionGetMultiOptions = Couchbase.Grpc.Protocol.Transactions.TransactionGetMultiOptions;

namespace Couchbase.FitPerformer
{
    internal class TwoWayTransaction
    {

        private volatile System.Exception? _testFailure;
        private volatile TransactionGetResult? _stashedGetResult;
        private readonly ConcurrentDictionary<int, TransactionGetResult> _stashedGetResults = new();
        private readonly ConcurrentDictionary<string, CountdownEvent> _latches = new ConcurrentDictionary<string, CountdownEvent>();
        private readonly CancellationToken _cancellationToken;
        private static Counters _counters;

        public TwoWayTransaction(CancellationToken token)
        {
            _cancellationToken = token;
        }

        public async Task<Couchbase.Grpc.Protocol.Transactions.TransactionResult> Run(Transactions transactions, TransactionCreateRequest request, ClusterConnection connection, global::Grpc.Core.IServerStreamWriter<TransactionStreamPerformerToDriver>? responseStream = null)
        {
            foreach (var latch in request.Latches)
            {
                var countDownLatch = new CountdownEvent(latch.InitialCount);
                _latches.TryAdd(latch.Name, countDownLatch);
            }
            var ptc = PerTransactionConfigBuilder.Create().Build();
            HooksUtil.ConfigureHooks(request.Options.Hook, connection, transactions);
            if (request.Options.HasTimeoutMillis)
            {
                ptc.Timeout = TimeSpan.FromMilliseconds(request.Options.TimeoutMillis);
            }

            if (request.Options.HasDurability)
            {
                ptc.DurabilityLevel = request.Options.Durability switch
                {
                    Durability.Majority => DurabilityLevel.Majority,
                    Durability.None => DurabilityLevel.None,
                    Durability.PersistToMajority => DurabilityLevel.PersistToMajority,
                    Durability.MajorityAndPersistToActive => DurabilityLevel.MajorityAndPersistToActive,
                    _ => throw new ArgumentOutOfRangeException(nameof(request.Options.Durability),
                        request.Options.Durability, "no matching mapping to DurabilityLevel")
                };
            }

            if (request.Options.MetadataCollection != null)
            {
                ptc.MetadataCollection = TxnOptionsUtil.ConvertCollectionToKeyspace(request.Options.MetadataCollection);
            }
            var attemptCount = -1;
            try
            {

                var txnResult = await transactions.RunAsync(async ctx =>
                {
                    var count = ++attemptCount;
                    Serilog.Log.Debug("{Name} Starting Attempt {Attempt}", request.Name, count);

                    var attemptToUse = Math.Min(count, request.Attempts.Count - 1);

                    var attempt = request.Attempts[attemptToUse];

                    foreach (var command in attempt.Commands)
                    {
                        await PerformOperation(request.Name, ctx, command, connection, responseStream).ConfigureAwait(false);
                    }

                    Serilog.Log.Debug("{Name} Completed Attempt {Attempt}", request.Name, count);

                }, ptc).ConfigureAwait(false);

                Serilog.Log.Debug("{Name} Transaction with ID {TxnId} Completed without Failure", request.Name, txnResult.TransactionId);
                return TxnResultsUtil.CreateResult(txnResult, null, transactions.CleanupQueueLength);
            }
            catch (TransactionFailedException ex)
            {
                Console.WriteLine($"Transaction Failed with: {ex}");
                if (_testFailure != null)
                {
                    Serilog.Log.Error("Test failed due to exception: {Ex}", ex);
                    throw _testFailure;
                }

                return TxnResultsUtil.CreateResult(null, ex, transactions.CleanupQueueLength);
            }
        }

        public void HandleRequest(CommandSetLatch setLatchCommand)
        {
            if (_latches.TryGetValue(setLatchCommand.LatchName, out var latch))
            {
                Serilog.Log.Debug("Signalling latch: {Name}", setLatchCommand.LatchName);
                latch.Signal();
            }
            else
            {
                throw new KeyNotFoundException(setLatchCommand.LatchName);
            }
        }
        private async Task PerformOperation(String testName, AttemptContext ctx, TransactionCommand command, ClusterConnection connection, global::Grpc.Core.IServerStreamWriter<TransactionStreamPerformerToDriver>? responseStream = null)
        {
            Serilog.Log.Debug("{Name} Performing TransactionCommand {Command}", testName, command.CommandCase);

            var onlyExpectedSuccessResult = new ExpectedResult {Success = true};

            switch (command.CommandCase)
            {
                case TransactionCommand.CommandOneofCase.Insert:
                {
                    var insertRequest = command.Insert;
                    var collection = await connection.GetCollectionAsync(insertRequest.DocId)
                        .ConfigureAwait(false);
                    var options = TransactionInsertOptionsBuilder.Default;
                    options.Transcoder(
                        OptionsUtil.GetTranscoder(insertRequest.Options?.Transcoder?.TranscoderCase));
                    if (insertRequest.Options?.Expiry != null)
                    {
                        if (insertRequest.Options.Expiry.HasRelativeSecs)
                        {
                            options.Expiry(
                                TimeSpan.FromSeconds(insertRequest.Options.Expiry.RelativeSecs));
                        }

                        if (insertRequest.Options.Expiry.HasAbsoluteEpochSecs)
                        {
                            options.Expiry(TimeSpan.FromSeconds(insertRequest.Options.Expiry
                                .AbsoluteEpochSecs));
                        }
                    }
                    var isBinary = insertRequest.Content?.ByteArray?.ToByteArray() != null;

                    await PerformOperation(testName, "insert", ctx, command.DoNotPropagateError,
                        insertRequest.ExpectedResult,
                        async () =>
                        {
                            await ctx.InsertAsync(collection, insertRequest.DocId.DocId_,
                                    isBinary
                                        ? insertRequest?.Content?.ByteArray?.ToByteArray()
                                        : JObject.Parse(insertRequest.ContentJson)
                                    , options
                                )
                                .ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.Replace:
                {
                    var replaceRequest = command.Replace;
                    ICouchbaseCollection? collection;
                    TransactionGetResult? getResult;
                    if (replaceRequest.HasUseStashedSlot)
                    {
                        getResult = _stashedGetResults[replaceRequest.UseStashedSlot];
                    }
                    else if (replaceRequest.UseStashedResult)
                    {
                        getResult = _stashedGetResult;
                    } else {
                        collection = await connection.GetCollectionAsync(replaceRequest.DocId);
                        getResult = await ctx
                            .GetAsync(collection, replaceRequest.DocId.DocId_);
                    }
                    var options = TransactionReplaceOptionsBuilder.Default;
                    Serilog.Log.Debug("default options expiry: {Expiry}", options.Build().Expiry);
                    options.Transcoder(
                        OptionsUtil.GetTranscoder(replaceRequest.Options?.Transcoder?.TranscoderCase));
                    if (replaceRequest.Options?.Expiry != null)
                    {
                        if (replaceRequest.Options.Expiry.HasRelativeSecs)
                        {
                            options.Expiry(
                                TimeSpan.FromSeconds(replaceRequest.Options.Expiry.RelativeSecs));
                        }

                        if (replaceRequest.Options.Expiry.HasAbsoluteEpochSecs)
                        {
                            options.Expiry(TimeSpan.FromSeconds(replaceRequest.Options.Expiry
                                .AbsoluteEpochSecs));
                        }
                    }

                    var isBinary = replaceRequest.Content?.ByteArray?.ToByteArray() != null;
                    await PerformOperation(testName, "replace", ctx, command.DoNotPropagateError,
                        replaceRequest.ExpectedResult, async () =>
                        {
                            {
                                await ctx.ReplaceAsync(getResult,
                                        isBinary
                                            ? replaceRequest.Content?.ByteArray?.ToByteArray()
                                            : JObject.Parse(replaceRequest.ContentJson)
                                        , options
                                    )
                                    .ConfigureAwait(false);
                            }

                        }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.Remove:
                {
                    var removeRequest = command.Remove;
                    ICouchbaseCollection? collection;
                    TransactionGetResult? getResult;
                    if (removeRequest.HasUseStashedSlot)
                    {
                        getResult = _stashedGetResults[removeRequest.UseStashedSlot];
                    }
                    else if (removeRequest.UseStashedResult)
                    {
                        getResult = _stashedGetResult;
                    }
                    else
                    {
                        collection = await connection.GetCollectionAsync(removeRequest.DocId);
                        getResult = await ctx.GetAsync(collection, removeRequest.DocId.DocId_)
                            .ConfigureAwait(false);
                    }
                    await PerformOperation(testName, "remove", ctx, command.DoNotPropagateError,
                        removeRequest.ExpectedResult, async () =>
                        {
                            await ctx.RemoveAsync(getResult).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.GetFromPreferredServerGroup:
                {
                    var getFromPreferredServerGroupRequest = command.GetFromPreferredServerGroup;
                    var collection =
                        await connection.GetCollectionAsync(
                            getFromPreferredServerGroupRequest.DocId);
                    await PerformOperation(testName, "getFromPreferredServerGroup", ctx,
                        command.DoNotPropagateError,
                        getFromPreferredServerGroupRequest.ExpectedResult,
                        async () =>
                        {
                            var getResult =
                                await ctx.GetReplicaFromPreferredServerGroup(collection,
                                    getFromPreferredServerGroupRequest.DocId.DocId_).ConfigureAwait(false);
                            HandleGetResult(getFromPreferredServerGroupRequest, getResult);
                        }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.Get:
                {
                    var getRequest = command.Get;
                    var collection = await connection.GetCollectionAsync(getRequest.DocId)
                        .ConfigureAwait(false);

                    await PerformOperation(testName, "get", ctx, command.DoNotPropagateError,
                        getRequest.ExpectedResult, async () =>
                        {
                            try
                            {
                                TransactionGetResult getResult = await ctx
                                    .GetAsync(collection, getRequest.DocId.DocId_)
                                    .ConfigureAwait(false);
                                HandleGetResult(getRequest, getResult);
                            }
                            catch (DocumentNotFoundException err)
                            {
                                // a bit of a hack duplicating Transactions.Run() logic, but this is a special case
                                // since the spec calls for Get to throw DocumentNotFoundException specifically.
                                throw ErrorBuilder.CreateError(ctx, ErrorClass.FailDocNotFound, err)
                                    .Build();
                            }

                        }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.GetOptional:
                {
                    var getOptionalRequest = command.GetOptional;
                    var req = getOptionalRequest.Get;
                    var collection = await connection.GetCollectionAsync(req.DocId)
                        .ConfigureAwait(false);

                    await PerformOperation(testName, "get optional", ctx,
                        command.DoNotPropagateError, req.ExpectedResult, async () =>
                        {
                            var getResult = await ctx.GetOptionalAsync(collection, req.DocId.DocId_)
                                .ConfigureAwait(false);
                            if (getResult == null && getOptionalRequest.ExpectDocPresent)
                            {
                                throw new TestFailureException(
                                    "Expected optional get to be present, but is not");
                            }
                            else if (getResult != null && !getOptionalRequest.ExpectDocPresent)
                            {
                                throw new TestFailureException(
                                    "Did not expect optional get to be present, but it is");
                            }

                            if (getOptionalRequest.ExpectDocPresent)
                            {
                                HandleGetResult(req, getResult);
                            }
                        }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.GetMulti:
                {
                    var getMultiRequest = command.GetMulti;
                    var allowReplicas = getMultiRequest.GetMultiReplicasFromPreferredServerGroup;
                    var specs = new List<TransactionGetMultiSpecBase>();
                    foreach (var spec in getMultiRequest.Specs)
                    {
                        var (coll, id) = await CommandUtils
                            .DetermineLocation(spec.Location, connection, _counters).ConfigureAwait(false);
                        ITypeTranscoder? transcoder = null;
                        if (spec.Transcoder != null)
                        {
                            transcoder = OptionsUtil.GetTranscoder(spec.Transcoder.TranscoderCase);
                        }

                        specs.Add(allowReplicas
                            ? new TransactionGetMultiReplicaFromPreferredServerGroupSpec(coll,
                                id, transcoder)
                            : new TransactionGetMultiSpec(coll, id, transcoder));
                    }

                    TransactionGetMultiOptionsBuilderBase opts = allowReplicas
                        ? TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder.Default
                        : TransactionGetMultiOptionsBuilder.Default;
                    if (getMultiRequest.Options != null && getMultiRequest.Options.HasMode)
                    {
                        switch (getMultiRequest.Options.Mode)
                        {
                            case TransactionGetMultiOptions.Types.TransactionGetMultiMode
                                .PrioritiseLatency:
                                opts.Mode(TransactionGetMultiMode.PrioritizeLatency);
                                break;
                            case TransactionGetMultiOptions.Types.TransactionGetMultiMode
                                .DisableReadSkewDetection:
                                opts.Mode(TransactionGetMultiMode.DisableReadSkewDetection);
                                break;
                            case TransactionGetMultiOptions.Types.TransactionGetMultiMode
                                .PrioritiseReadSkewDetection:
                                opts.Mode(TransactionGetMultiMode.PrioritizeReadSkewDetection);
                                break;
                        }
                    }

                    await PerformOperation(testName, "getMulti", ctx, command.DoNotPropagateError,
                        getMultiRequest.ExpectedResult,
                        async () =>
                        {
                            TransactionGetMultiResultBase result = allowReplicas
                                ? await ctx.GetMultiReplicaFromPreferredServerGroup(
                                        specs.Cast<

                                                TransactionGetMultiReplicaFromPreferredServerGroupSpec>()
                                            .ToList(),
                                        (TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder)
                                        opts)
                                    .ConfigureAwait(false)
                                : await ctx.GetMulti(
                                    specs.Cast<TransactionGetMultiSpec>().ToList(),
                                    (TransactionGetMultiOptionsBuilder)opts).ConfigureAwait(false);
                            // compare expected content
                            foreach (var spec in getMultiRequest.Specs)
                            {
                                var exists = result.Exists(spec.ExpectedResultPosition);
                                if (spec.ExpectPresent && exists)
                                {
                                    TxnResultsUtil.ProcessExpectedContent(spec.ContentAsValidation,
                                        result.GetMultiSpecResult(spec.ExpectedResultPosition));
                                    continue;
                                }

                                if (!spec.ExpectPresent && !exists)
                                {
                                    // this is fine
                                    continue;
                                }

                                throw new TestFailureException(
                                    $"ExpectPresent = {spec.ExpectPresent} and Exists = {exists}");
                            }
                        }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.Commit:
                {
                    await ctx.CommitAsync().ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.Rollback:
                {
                    await ctx.RollbackAsync().ConfigureAwait(false);
                    break;
                }

                case TransactionCommand.CommandOneofCase.InsertRegularKv:
                {
                    var insertRegularKvRequest = command.InsertRegularKv;
                    var collection = await connection.GetCollectionAsync(insertRegularKvRequest.DocId).ConfigureAwait(false);

                    var content = JObject.Parse(insertRegularKvRequest.ContentJson);
                    await PerformOperation(testName, "KV insert", ctx, command.DoNotPropagateError, new List<ExpectedResult> { onlyExpectedSuccessResult }, async () =>
                    {
                        await collection.InsertAsync(insertRegularKvRequest.DocId.DocId_, content).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.ReplaceRegularKv:
                {
                    var replaceRegularKvRequest = command.ReplaceRegularKv;
                    var collection = await connection.GetCollectionAsync(replaceRegularKvRequest.DocId).ConfigureAwait(false);

                    var content = JObject.Parse(replaceRegularKvRequest.ContentJson);
                    await PerformOperation(testName, "KV replace", ctx, command.DoNotPropagateError, new List<ExpectedResult> { onlyExpectedSuccessResult }, async () =>
                    {
                        await collection.ReplaceAsync(replaceRegularKvRequest.DocId.DocId_, content).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.RemoveRegularKv:
                {
                    var removeRegularKvRequest = command.RemoveRegularKv;
                    var collection = await connection.GetCollectionAsync(removeRegularKvRequest.DocId).ConfigureAwait(false);

                    await PerformOperation(testName, "KV remove", ctx, command.DoNotPropagateError, new List<ExpectedResult> { onlyExpectedSuccessResult }, async () =>
                    {
                        var opts = new KeyValue.RemoveOptions().Durability(KeyValue.DurabilityLevel.MajorityAndPersistToActive);
                        await collection.RemoveAsync(removeRegularKvRequest.DocId.DocId_, opts).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.WaitOnLatch:
                {
                    var waitOnLatchRequest = command.WaitOnLatch;

                    await PerformOperation(testName, "wait on latch " + waitOnLatchRequest.LatchName, ctx, command.DoNotPropagateError, new List<ExpectedResult> { onlyExpectedSuccessResult }, () =>
                    {
                        var latch = _latches[waitOnLatchRequest.LatchName];
                        latch.Wait(_cancellationToken);
                        Serilog.Log.Debug("Passed latch {Name}", waitOnLatchRequest.LatchName);
                        return Task.CompletedTask;
                    }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.SetLatch:
                {
                    var setLatchRequest = command.SetLatch;

                    await PerformOperation(testName, "set latch " + setLatchRequest.LatchName, ctx, command.DoNotPropagateError, new List<ExpectedResult> { onlyExpectedSuccessResult }, async () =>
                    {
                        var latch = _latches[setLatchRequest.LatchName];
                        try
                        {
                            latch.Signal();
                            if (responseStream != null)
                            {
                                await responseStream.WriteAsync(new TransactionStreamPerformerToDriver()
                                {
                                    Broadcast = new BroadcastToOtherConcurrentTransactionsRequest()
                                    {
                                        LatchSet = new CommandSetLatch()
                                        {
                                            LatchName = setLatchRequest.LatchName
                                        }
                                    }
                                }).ConfigureAwait(false);
                            }
                        }
                        catch (InvalidOperationException)
                        {
                            // The driver expects the Java behavior of countdown latch, which silently ignores counting down below zero.
                        }


                    }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.Parallelize:
                {
                    var parallelizeRequest = command.Parallelize;

                    List<Task> tasks = new List<Task>();
                    int parallelism = parallelizeRequest.Parallelism;
                    foreach (var innerCommand in parallelizeRequest.Commands)
                    {
                        Serilog.Log.Debug("Parallelizing {Cmd} with pism = {P}",
                            innerCommand.CommandCase, parallelizeRequest.Parallelism);
                        var task = Task.Run(async () =>
                        {
                            await PerformOperation(testName, ctx, innerCommand, connection,
                                responseStream).ConfigureAwait(false);
                        });
                        tasks.Add(task);
                        if (tasks.Count >= parallelism)
                        {
                            Serilog.Log.Debug("{T} tasks, awaiting them now...", tasks.Count);
                            await Task.WhenAll(tasks).ConfigureAwait(false);
                            tasks.Clear();
                        }
                    }

                    if (tasks.Count > 0)
                    {
                        await Task.WhenAll(tasks).ConfigureAwait(false);
                    }

                    break;
                }
                case TransactionCommand.CommandOneofCase.ThrowException:
                {
                    throw new TestFailOtherException();
                }
                case TransactionCommand.CommandOneofCase.Query:
                {
                    var queryRequest = command.Query;
                    await PerformOperation(testName, "query", ctx, command.DoNotPropagateError, queryRequest.ExpectedResult, async () =>
                    {
                        KeyValue.IScope scope = null;
                        if (queryRequest.Scope != null)
                        {
                            var bkt = await connection.GetBucketAsync(queryRequest.Scope.BucketName);
                            scope = bkt.Scope(queryRequest.Scope.ScopeName);
                            Serilog.Log.Information("Using Custom Scope in this Query : {ScopeName}", scope.Name);
                        }

                        var opts = TxnOptionsUtil.ConvertTransactionQueryOptions(queryRequest.QueryOptions);

                        Serilog.Log.Debug("Query: {Query} with {Opts}", queryRequest.Statement, opts);
                        var qr = await ctx.QueryAsync<object>(queryRequest.Statement, opts, scope);
                        await ResultValidation.ValidateQueryResult(queryRequest, qr);
                    }).ConfigureAwait(false);
                    break;
                }
                case TransactionCommand.CommandOneofCase.TestFail:
                    var ex = new InternalPerformerException("Should not reach here", new TestFailOtherException());
                    _testFailure = ex;
                    throw ex;
                default:
                    throw new NotSupportedException("The command " + command.CommandCase + "is not yet implemented");
            }
        }

        private void HandleGetResult(CommandGet request, TransactionGetResult getResult)
        {
            _stashedGetResult = getResult;
            if (request.HasStashInSlot)
            {
                Serilog.Log.Debug("Stashing in slot {Slot}", request.StashInSlot);
                _stashedGetResults[request.StashInSlot] = getResult;
            }

            if (string.IsNullOrEmpty(request.ExpectedContentJson)) return;

            var expected = JObject.Parse(request.ExpectedContentJson);
            var actual = getResult.ContentAs<JObject>();
            if (JToken.DeepEquals(expected, actual)) return;

            Serilog.Log.Warning("Expected content {Expected}, Actual {Actual}", expected.ToString(), actual?.ToString());
            throw new TestFailureException("Did not get expected content from get result");
        }

        private void HandleGetResult(CommandGetReplicaFromPreferredServerGroup request,
            TransactionGetResult? getResult)
        {
            _stashedGetResult = getResult;
            if (request.HasStashInSlot && getResult != null)
            {
                Serilog.Log.Debug("Stashing in slot {Slot}", request.StashInSlot);
                _stashedGetResults[request.StashInSlot] = getResult;
            }

            if (request.ContentAsValidation == null) return;

            if (request.ContentAsValidation.ExpectedContentBytes.ToByteArray()
                .SequenceEqual(getResult?.ContentAs<byte[]>() ?? Array.Empty<byte>())) return;

            throw new TestFailureException("Expected bytes not equal to bytes in getResult");
        }
        private async Task PerformOperation(String testName, String opDebug, AttemptContext ctx, bool doNotPropagateError, ICollection<ExpectedResult> expectedResults, Func<Task> op)
        {
            try
            {
                Serilog.Log.Debug("{Tid}.{Atmpt}: Performing command: {OpDebug}", ctx.TransactionId,
                    ctx.AttemptId, opDebug);
                await op().ConfigureAwait(false);

                var success = new ExpectedResult { Success = true };
                if (AnythingAllowed(expectedResults)) return;
                // NOTE there wasn't any handling of _empty_ expectedResults
                if (expectedResults.Count > 0 && !HasResult(expectedResults, success))
                {
                    Serilog.Log.Warning("{Name} Did not expect success, but op succeeded",
                        testName);
                    var e = new TestFailureException("Operation succeeded, but did not expect it");
                    _testFailure = e;
                    throw e;
                }

                Serilog.Log.Debug("{Tid}.{Atmpt}: {Name} Command Succeeded", ctx.TransactionId,
                    ctx.AttemptId, testName);
            }
            catch (Exception ex) when (ex is DocumentUnretrievableException or FeatureNotAvailableException)
            {
                // check to see if we expected this...
                if (AnythingAllowed(expectedResults))
                {
                    if (doNotPropagateError)
                        return;
                    throw;
                }

                var exception = (ex is DocumentUnretrievableException)
                    ? ExternalException.DocumentUnretrievableException
                    : ExternalException.FeatureNotAvailableException;

                var expected = new ExpectedResult
                {
                    Success = false, Exception = exception
                };
                if (expectedResults.Count > 0 && !HasResult(expectedResults, expected))
                    throw new TestFailureException("Did not have expected results {expectedResults}");

                // ok we expected this, yay
                if (doNotPropagateError)
                    return;
                throw;
            }
            catch (TestFailureException ex)
            {
                Serilog.Log.Debug("{Tid}.{Atmpt}: {Name} TestFailureException: {Ex}", ctx.TransactionId, ctx.AttemptId, testName, ex);
                _testFailure = ex;
                // we don't fail a test if there's a _testFailure, so we have to re-throw to make test fail.
                throw;
            }
            catch (InternalPerformerException ex)
            {
                Serilog.Log.Debug("{Tid}.{Atmpt}: {Name} InternalPerformerException: {Ex}", ctx.TransactionId, ctx.AttemptId, testName, ex);
                _testFailure = ex;
                // we don't fail a test if there's a _testFailure, so we have to re-throw to make test fail.
                throw;
            }
            catch (TransactionOperationFailedException ex)
            {
                if (AnythingAllowed(expectedResults))
                {
                    Serilog.Log.Debug("{Name} Got Error {@Exception}, but anything allowed so not failing the test. doNotPropagateError={Dnp}", testName, ex, doNotPropagateError);
                    if (doNotPropagateError)
                    {
                        return;
                    }

                    throw;
                }

                var ew = new ErrorWrapper
                {
                    AutoRollbackAttempt = ex.AutoRollbackAttempt,
                    RetryTransaction = ex.RetryTransaction
                };

                var innerCause = ex.Cause ?? ex.InnerException;
                var externalException = TxnResultsUtil.MapCause(innerCause);
                var expectedCause = new ExpectedCause { Exception = TxnResultsUtil.MapCause(innerCause) };
                ew.Cause = expectedCause;

                if (externalException == ExternalException.Unknown)
                {
                    Serilog.Log.Debug("{Name} ExternalException.Unknown: {Ex}", testName, innerCause);
                }

                ew.ToRaise = TxnResultsUtil.MapToRaise(ex.FinalErrorToRaise);

                var ok = false;
                foreach (var er in expectedResults)
                {
                    if (er.ResultCase == ExpectedResult.ResultOneofCase.Error)
                    {
                        var m = er.Error;
                        if (m.AutoRollbackAttempt == ew.AutoRollbackAttempt
                            && m.RetryTransaction == ew.RetryTransaction
                            && m.ToRaise == ew.ToRaise)
                        {
                            var c = m.Cause;

                            if (c.DoNotCheck || c.Equals(ew.Cause))
                            {
                                ok = true;
                            }
                        }
                    }
                }

                if (!ok)
                {
                    Serilog.Log.Warning("{Name} Operation failed unexpectedly, was expecting {@ExpectedResults} but got {@ActualResults}: {Msg}", testName, expectedResults, ew, ex.Message);
                    System.Exception e = new TestFailureException($"{testName} Operation failed unexpectedly, was expecting  one of\n{expectedResults}\nbut got\n{ew}: {ex.Message}", ex);
                    _testFailure = e;
                    if (doNotPropagateError)
                    {
                        return;
                    }

                    throw e;
                }
                Serilog.Log.Debug("{TestName} Operation '{Name}' failed as expected", testName, opDebug);

                if (AnythingAllowed(expectedResults))
                {
                    Serilog.Log.Debug("{Name} Got Error {@Exception}, but anything allowed, so not failing test. doNotPropagate={Dnp}", testName, ex, doNotPropagateError);
                    if (!doNotPropagateError)
                    {
                        throw;
                    }
                }
                else
                {
                    if (!doNotPropagateError)
                    {
                        throw;
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (ex is ForwardCompatibilityFailureException fce)
                {
                    Serilog.Log.Warning("Forward Compatibility Failure: {Message}", fce.Message);
                }

                var ok = AnythingAllowed(expectedResults);

                if (!ok)
                {
                    foreach (var er in expectedResults)
                    {
                        var ee = TxnResultsUtil.MapCause(ex);
                        var expected = er.Exception;
                        if (expected != ExternalException.Unknown && ee == expected)
                        {
                            Serilog.Log.Information("{TestName} Operation '{Name}' failed as expected",
                                testName, opDebug);
                            ok = true;
                            break;
                        }
                    }
                    // ok could have changed, so check it.
                    if (!ok)
                    {
                        Serilog.Log.Warning("{Name} Got exception {Exception}", testName, ex);

                        var ipf = new InternalPerformerException(
                            $"Command should only raise TransactionOperationFailedException but did {ex.GetType().Name}",
                            ex);
                        _testFailure = ipf;
                        throw ipf;
                    }
                }

                if (doNotPropagateError)
                {
                    return;
                }

                throw;
            }

        }

        private static bool HasResult(ICollection<ExpectedResult> expectedResults, ExpectedResult er)
        {
            return expectedResults.Contains(er);
        }

        private static bool AnythingAllowed(ICollection<ExpectedResult> expectedResults)
        {
            if (expectedResults.Count == 0) return true;
            var allowed = new ExpectedResult {AnythingAllowed = true};
            return expectedResults.Contains(allowed);
        }
    }
}

internal class TestFailureException : System.Exception
{
    public TestFailureException(string? message) : base(message)
    {
    }

    public TestFailureException(string message, System.Exception e) : base(message, e)
    {
    }
}
