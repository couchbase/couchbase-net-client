#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Utils;
using Couchbase.Integrated.Transactions.ActiveTransactionRecords;
using Couchbase.Integrated.Transactions.Cleanup;
using Couchbase.Integrated.Transactions.Components;
using Couchbase.Integrated.Transactions.Config;
using Couchbase.Integrated.Transactions.DataAccess;
using Couchbase.Integrated.Transactions.DataModel;
using Couchbase.Integrated.Transactions.Error;
using Couchbase.Integrated.Transactions.Error.Attempts;
using Couchbase.Integrated.Transactions.Error.External;
using Couchbase.Integrated.Transactions.Error.Internal;
using Couchbase.Integrated.Transactions.Forwards;
using Couchbase.Integrated.Transactions.Internal;
using Couchbase.Integrated.Transactions.Internal.Test;
using Couchbase.Integrated.Transactions.LogUtil;
using Couchbase.Integrated.Transactions.Support;
using Couchbase.Integrated.Transactions.Util;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Couchbase.Integrated.Transactions
{
    /// <summary>
    /// Provides methods that allow an application's transaction logic to read, mutate, insert, and delete documents.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public class AttemptContext
    {
        private static readonly TimeSpan ExpiryThreshold = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan WriteWriteConflictTimeLimit = TimeSpan.FromSeconds(1);
        private readonly TransactionContext _overallContext;
        private readonly MergedTransactionConfig _config;
        private readonly TestHookMap _testHooks;
        internal IRedactor Redactor { get; }
        private AttemptStates _state = AttemptStates.NOTHING_WRITTEN;
        private readonly ErrorTriage _triage;

        private readonly StagedMutationCollection _stagedMutations = new StagedMutationCollection();
        private readonly object _initAtrLock = new();
        private AtrRepository? _atr = null;
        private readonly DocumentRepository _docs;
        private readonly DurabilityLevel _effectiveDurabilityLevel;
        private readonly List<MutationToken> _finalMutations = new List<MutationToken>();
        private readonly ConcurrentDictionary<long, TransactionOperationFailedException> _previousErrors = new ConcurrentDictionary<long, TransactionOperationFailedException>();
        private bool _expirationOvertimeMode = false;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ICluster _cluster;
        private readonly ITypeSerializer _nonStreamingTypeSerializer;
        private readonly IRequestTracer _requestTracer;
        private bool _queryMode = false;
        private Uri? _lastDispatchedQueryNode = null;
        private bool _singleQueryTransactionMode = false;
        private IScope? _queryContextScope = null;

        // ExtThreadSafety lock
        private readonly SemaphoreSlim _mutex = new(1, 1);
        private readonly WaitGroup _kvOps = new();

        /// <summary>
        /// Gets the ID of this individual attempt.
        /// </summary>
        public string AttemptId { get; }

        /// <summary>
        /// Gets the ID of this overall transaction.
        /// </summary>
        public string TransactionId => _overallContext.TransactionId;

        internal bool UnstagingComplete { get; private set; } = false;

        internal AttemptContext(
            TransactionContext overallContext,
            string attemptId,
            TestHookMap? testHooks,
            IRedactor redactor,
            ILoggerFactory loggerFactory,
            ICluster cluster,
            DocumentRepository? documentRepository = null,
            AtrRepository? atrRepository = null,
            IRequestTracer? requestTracer = null,
            bool singleQueryTransactionMode = false)
        {
            _cluster = cluster;
            _nonStreamingTypeSerializer = NonStreamingSerializerWrapper.FromCluster(_cluster);
            _requestTracer = requestTracer ?? new NoopRequestTracer();
            AttemptId = attemptId ?? throw new ArgumentNullException(nameof(attemptId));
            _overallContext = overallContext ?? throw new ArgumentNullException(nameof(overallContext));
            _config = _overallContext.Config;
            _testHooks = testHooks ?? new();
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _effectiveDurabilityLevel = _config.DurabilityLevel;
            _loggerFactory = loggerFactory;
            Logger = loggerFactory.CreateLogger<AttemptContext>();
            _triage = new ErrorTriage(this, loggerFactory);
            _docs = documentRepository ?? new DocumentRepository(_overallContext, _effectiveDurabilityLevel, AttemptId, _nonStreamingTypeSerializer);
            _singleQueryTransactionMode = singleQueryTransactionMode;
            if (atrRepository != null)
            {
                _atr = atrRepository;
            }
        }

        private async Task LockAsync([CallerMemberName] string lockDebug = "")
        {
            AssertNotLocked(lockDebug);
            Logger.LogDebug("Locking ({lockDebug})", lockDebug);
            var waitTime = _overallContext.RemainingUntilExpiration;
            if (waitTime < TimeSpan.Zero)
            {
                waitTime = TimeSpan.Zero;
            }
            var successfullyWaited = await _mutex.WaitAsync(waitTime).CAF();
            if (!successfullyWaited)
            {
                throw Error(ec: ErrorClass.FailExpiry,
                    err: new AttemptExpiredException(this, $"Expired while under async Lock ({lockDebug})"),
                    raise: TransactionOperationFailedException.FinalError.TransactionExpired, rollback: false, setStateBits: true);
            }
        }

        [Conditional("DEBUG")]
        private void AssertNotLocked(string lockDebug)
        {
            if (_mutex.CurrentCount == 0)
            {
                throw new InvalidOperationException($"{lockDebug} tried to lock when already under lock.");
            }
        }

        private void Unlock([CallerMemberName] string lockDebug = "")
        {
            Logger.LogDebug("Unlocking ({lockDebug})", lockDebug);
            // per the spec, unlocking needs to neither block nor throw if the mutex is not currently locked.
            // therefore, the mutex is created using the 2-argument overload of SemaphorSlim(1,1),
            // as SemaphoreSlim(1) increment the CurrentCount on extra Release()
            try
            {
                _mutex.Release();
            }
            catch (SemaphoreFullException)
            {
                // ignore this exception, since we Unlock-when-not-locked mutex behavior
            }
        }

        private async Task<WaitGroup.Waiter> LockAndAddKvOp(string dbg)
        {
            await LockAsync(dbg).CAF();
            var waiter = _kvOps.Add(dbg);
            return waiter;
        }

        private async Task<WaitGroup.Waiter> KvOpInit([CallerMemberName] string stageName = "")
        {
            var waiter = await LockAndAddKvOp(stageName).CAF();
            DoneCheck();
            BailoutIfInOvertime();
            return waiter;
        }

        private async Task WaitForKvAndLock(int depth = 0, [CallerMemberName] string callerName = "")
        {
            var initialCount = _kvOps.RunningTotal;
            Logger.LogDebug("{methodName}, initialCount={initialCount}, depth={depth}",
                nameof(WaitForKvAndLock),
                initialCount,
                depth);
            var completed = await _kvOps.TryWhenAll(_overallContext.RemainingUntilExpiration).CAF();
            if (!completed)
            {
                Logger.LogWarning("{methodName}: expired while waiting, remaining={remainingCount}\n{waitingOps}",
                    nameof(WaitForKvAndLock), _kvOps.RunningTotal, _kvOps);
                Error(
                    ec: ErrorClass.FailExpiry,
                    raise: TransactionOperationFailedException.FinalError.TransactionExpired,
                    rollback: false,
                    err: new AttemptExpiredException(this, callerName),
                    setStateBits: true
                    );
            }

            await LockAsync($"{callerName}:{nameof(WaitForKvAndLock)}").CAF();
            var afterWhenAllCount = _kvOps.RunningTotal;
            if (initialCount != afterWhenAllCount)
            {
                Logger.LogDebug("{methodName}: KV ops added while waiting for KV ops to finish. updatedCount={updatedCount}",
                    nameof(WaitForKvAndLock), afterWhenAllCount);
                Unlock();
                await WaitForKvAndLock(depth: ++depth, callerName).CAF();
            }

            Logger.LogDebug("All KV ops finished, continuing under lock ({callerName})", callerName);
        }

        private void UnlockOnError([CallerMemberName] string callerName = "")
        {
            Logger.LogInformation("[{attemptId}] Unlocking after error ({callerName}).", AttemptId, callerName);
            Unlock(callerName);
        }

        private void RemoveKvOp(string operationId)
        {
            if (!_kvOps.TryRemoveOp(operationId))
            {
                Logger.LogWarning("Failed to remove {operationId}", operationId);
            }
        }

        /// <summary>
        /// Gets the logger instance used for this AttemptContext.
        /// </summary>
        public ILogger<AttemptContext> Logger { get; }

        /// <summary>
        /// Gets a document.
        /// </summary>
        /// <param name="collection">The collection to look up the document in.</param>
        /// <param name="id">The ID of the document.</param>
        /// <returns>A <see cref="TransactionGetResult"/> containing the document.</returns>
        /// <exception cref="DocumentNotFoundException">If the document does not exist.</exception>
        public async Task<TransactionGetResult> GetAsync(ICouchbaseCollection collection, string id)
        {
            var getResult = await GetOptionalAsync(collection, id).CAF();
            if (getResult == null)
            {
                throw new DocumentNotFoundException();
            }

            return getResult;
        }

        /// <summary>
        /// Gets a document or null.
        /// </summary>
        /// <param name="collection">The collection to look up the document in.</param>
        /// <param name="id">The ID of the document.</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="TransactionGetResult"/> containing the document, or null if  not found.</returns>
        public Task<TransactionGetResult?> GetOptionalAsync(ICouchbaseCollection collection, string id, IRequestSpan? parentSpan = null)
            => _queryMode ? GetWithQuery(collection, id, parentSpan) : GetWithKv(collection, id, parentSpan);

        private async Task<TransactionGetResult?> GetWithKv(ICouchbaseCollection collection, string id, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            DoneCheck();
            CheckErrors();
            CheckExpiryAndThrow(id, hookPoint: DefaultTestHooks.HOOK_GET);

            /*
             * Check stagedMutations.
               If the doc already exists in there as a REPLACE or INSERT return its post-transaction content in a TransactionGetResult.
                Protocol 2.0 amendment: and TransactionGetResult::links().isDeleted() reflecting whether it is a tombstone or not.
               Else if the doc already exists in there as a remove, return empty.
             */
            var staged = _stagedMutations.Find(collection, id);
            if (staged != null)
            {
                switch (staged.Type)
                {
                    case StagedMutationType.Insert:
                    case StagedMutationType.Replace:
                        // LOGGER.info(attemptId, "found own-write of mutated doc %s", RedactableArgument.redactUser(id));
                        return TransactionGetResult.FromOther(staged.Doc, new JObjectContentWrapper(staged.Content));
                    case StagedMutationType.Remove:
                        // LOGGER.info(attemptId, "found own-write of removed doc %s", RedactableArgument.redactUser(id));
                        return null;
                    default:
                        throw new InvalidOperationException($"Document '{Redactor.UserData(id)}' was staged with type {staged.Type}");
                }
            }

            try
            {
                try
                {
                    _testHooks.Sync(HookPoint.BeforeDocGet, this, id);

                    var result = await GetWithMav(collection, id, parentSpan: traceSpan.Item).CAF();

                    _testHooks.Sync(HookPoint.AfterGetComplete, this, id);
                    await ForwardCompatibility.Check(this, ForwardCompatibility.Gets, result?.TransactionXattrs?.ForwardCompatibility).CAF();
                    return result;
                }
                catch (Exception ex)
                {
                    var tr = _triage.TriageGetErrors(ex);
                    switch (tr.ec)
                    {
                        case ErrorClass.FailDocNotFound:
                            return TransactionGetResult.Empty;
                        default:
                            throw _triage.AssertNotNull(tr, ex);
                    }
                }
            }
            catch (TransactionOperationFailedException toSave)
            {
                SaveErrorWrapper(toSave);
                throw;
            }
        }

        private async Task<TransactionGetResult?> GetWithQuery(ICouchbaseCollection collection, string id, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            try
            {
                var queryOptions = NonStreamingQuery().Parameter(collection.MakeKeyspace())
                                                      .Parameter(id);
                using var queryResult = await QueryWrapper<QueryGetResult>(0, _queryContextScope, "EXECUTE __get",
                    options: queryOptions,
                    hookPoint: DefaultTestHooks.HOOK_QUERY_KV_GET,
                    txdata: JObject.FromObject(new { kv = true }),
                    parentSpan: traceSpan.Item).CAF();

                var firstResult = await queryResult.FirstOrDefaultAsync().CAF();
                if (firstResult == null)
                {
                    return null;
                }

                var getResult = TransactionGetResult.FromQueryGet(collection, id, firstResult);
                Logger.LogDebug("GetWithQuery found doc (id = {id}, cas = {cas})", id, getResult.Cas);
                return getResult;
            }
            catch (TransactionOperationFailedException)
            {
                // If err is TransactionOperationFailed: propagate err.
                throw;
            }
            catch (Exception err)
            {
                if (err is DocumentNotFoundException)
                {
                    return null;
                }

                var classified = ErrorBuilder.CreateError(this, err.Classify(), err).Build();
                SaveErrorWrapper(classified);
                throw classified;
            }
        }

        private async Task<TransactionGetResult?> GetWithMav(ICouchbaseCollection collection, string id, string? resolveMissingAtrEntry = null, IRequestSpan? parentSpan = null)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // we need to resolve the state of that transaction. Here is where we do the “Monotonic Atomic View” (MAV) logic
            try
            {
                // Do a Sub-Document lookup, getting all transactional metadata, the “$document” virtual xattr,
                // and the document’s body. Timeout is set as in Timeouts.
                var docLookupResult = await _docs.LookupDocumentAsync(collection, id, fullDocument: true).CAF();
                Logger.LogDebug("{method} for {redactedId}, attemptId={attemptId}, postCas={postCas}",
                    nameof(GetWithMav), Redactor.UserData(id), AttemptId, docLookupResult.Cas);
                if (docLookupResult == null)
                {
                    return TransactionGetResult.Empty;
                }

                var blockingTxn = docLookupResult?.TransactionXattrs;
                if (blockingTxn?.Id?.AttemptId == null
                    || blockingTxn?.Id?.Transactionid == null
                    || blockingTxn?.AtrRef?.BucketName == null
                    || blockingTxn?.AtrRef?.CollectionName == null)
                {
                    // Not in a transaction, or insufficient transaction metadata
                    return docLookupResult!.IsDeleted
                        ? TransactionGetResult.Empty
                        : docLookupResult.GetPreTransactionResult();
                }

                if (resolveMissingAtrEntry == blockingTxn.Id?.AttemptId)
                {
                    // This is our second attempt getting the document, and it’s in the same state as before
                    return docLookupResult!.IsDeleted
                        ? TransactionGetResult.Empty
                        : docLookupResult.GetPostTransactionResult();
                }

                resolveMissingAtrEntry = blockingTxn.Id?.AttemptId;

                if (blockingTxn.Id?.AttemptId == this.AttemptId)
                {
                    // return post-transaction version.
                    return docLookupResult!.GetPostTransactionResult();
                }

                var getCollectionTask = _atr?.GetAtrCollection(blockingTxn.AtrRef)
                                        ?? AtrRepository.GetAtrCollection(blockingTxn.AtrRef, collection);
                var docAtrCollection = await getCollectionTask.CAF()
                                       ?? throw new ActiveTransactionRecordNotFoundException();

                var findEntryTask =  AtrRepository.FindEntryForTransaction(docAtrCollection, blockingTxn.AtrRef.Id!,
                                        blockingTxn.Id!.AttemptId);

                AtrEntry? atrEntry = null;
                try
                {
                    atrEntry = await findEntryTask.CAF()
                               ?? throw new ActiveTransactionRecordEntryNotFoundException();
                }
                catch (ActiveTransactionRecordEntryNotFoundException)
                {
                    // Recursively call this section from the top, passing resolvingMissingATREntry set to the attemptId of the blocking transaction.
                    return await GetWithMav(collection, id, resolveMissingAtrEntry = blockingTxn.Id!.AttemptId, traceSpan.Item).ConfigureAwait(false);
                }
                catch (Exception atrLookupException)
                {
                    var atrLookupTriage = _triage.TriageAtrLookupInMavErrors(atrLookupException);
                    throw _triage.AssertNotNull(atrLookupTriage, atrLookupException);
                }

                if (blockingTxn.Id!.AttemptId == AttemptId)
                {
                    if (blockingTxn.Operation?.Type == "remove")
                    {
                        return TransactionGetResult.Empty;
                    }
                    else
                    {
                        return docLookupResult!.GetPostTransactionResult();
                    }
                }

                await ForwardCompatibility
                    .Check(this, ForwardCompatibility.GetsReadingAtr, atrEntry.ForwardCompatibility).CAF();

                if (atrEntry.State == AttemptStates.COMMITTED || atrEntry.State == AttemptStates.COMPLETED)
                {
                    if (blockingTxn.Operation?.Type == "remove")
                    {
                        return TransactionGetResult.Empty;
                    }

                    return docLookupResult!.GetPostTransactionResult();
                }

                if (docLookupResult!.IsDeleted || blockingTxn.Operation?.Type == "insert")
                {
                    return TransactionGetResult.Empty;
                }

                return docLookupResult.GetPreTransactionResult();
            }
            catch (ActiveTransactionRecordNotFoundException ex)
            {
                Logger.LogWarning("ATR not found: {ex}", ex);
                if (resolveMissingAtrEntry == null)
                {
                    throw;
                }

                return await GetWithMav(collection, id, resolveMissingAtrEntry).CAF();
            }
            catch (ActiveTransactionRecordEntryNotFoundException ex)
            {
                Logger.LogWarning("ATR entry not found: {ex}", ex);
                if (resolveMissingAtrEntry == null)
                {
                    throw;
                }

                return await GetWithMav(collection, id, resolveMissingAtrEntry).CAF();
            }
            catch (SubDocException sdEx)
            {
                var ec = sdEx.Classify();
                switch (ec)
                {
                    case ErrorClass.FailDocNotFound:
                    case ErrorClass.FailPathNotFound:
                        throw new DocumentNotFoundException(sdEx.Context);
                }

                throw;
            }
        }

        private void CheckErrors()
        {
            /*
             * Before performing any operation, including commit, check if the errors member is non-empty.
             * If so, raise an Error(ec=FAIL_OTHER, cause=PreviousOperationFailed).
             */
            if (!_previousErrors.IsEmpty)
            {
                throw ErrorBuilder.CreateError(this, ErrorClass.FailOther)
                    .Cause(new PreviousOperationFailedException(_previousErrors.Values))
                    .Build();
            }
        }

        /// <summary>
        /// Replace the content of a document previously fetched in this transaction with new content.
        /// </summary>
        /// <param name="doc">The <see cref="TransactionGetResult"/> of a document previously looked up in this transaction.</param>
        /// <param name="content">The updated content.</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="TransactionGetResult"/> reflecting the updated content.</returns>
        public Task<TransactionGetResult> ReplaceAsync(TransactionGetResult doc, object content, IRequestSpan? parentSpan = null)
            => _queryMode ? ReplaceWithQuery(doc, content, parentSpan) : ReplaceWithKv(doc, content, parentSpan);

        private async Task<TransactionGetResult> ReplaceWithKv(TransactionGetResult doc, object content, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            DoneCheck();
            CheckErrors();

            var stagedOld = _stagedMutations.Find(doc);
            if (stagedOld?.Type == StagedMutationType.Remove)
            {
                throw ErrorBuilder.CreateError(this, ErrorClass.FailDocNotFound, new DocumentNotFoundException()).Build();
            }

            CheckExpiryAndThrow(doc.Id, DefaultTestHooks.HOOK_REPLACE);
            await CheckWriteWriteConflict(doc, ForwardCompatibility.WriteWriteConflictReplacing, traceSpan.Item).CAF();
            await InitAtrIfNeeded(doc.Collection, doc.Id, traceSpan.Item).CAF();
            await SetAtrPendingIfFirstMutation(doc.Collection, traceSpan.Item).CAF();

            if (stagedOld?.Type == StagedMutationType.Insert)
            {
                return await CreateStagedInsert(doc.Collection, doc.Id, content, stagedOld.Doc.Cas, traceSpan.Item).CAF();
            }

            return await CreateStagedReplace(doc, content, accessDeleted: doc.IsDeleted, parentSpan: traceSpan.Item).CAF();
        }
        private async Task<TransactionGetResult> ReplaceWithQuery(TransactionGetResult doc, object content, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            JObject txdata = TxDataForReplaceAndRemove(doc);

            try
            {
                var queryOptions = NonStreamingQuery().Parameter(doc.Collection.MakeKeyspace())
                                               .Parameter(doc.Id)
                                               .Parameter(content)
                                               .Parameter(new { });
                using var queryResult = await QueryWrapper<QueryGetResult>(0, _queryContextScope, "EXECUTE __update",
                    options: queryOptions,
                    hookPoint: DefaultTestHooks.HOOK_QUERY_KV_REPLACE,
                    txdata: txdata,
                    parentSpan: traceSpan.Item).CAF();

                var firstResult = await queryResult.FirstOrDefaultAsync().CAF();
                if (firstResult == null)
                {
                    throw new DocumentNotFoundException();
                }

                var getResult = TransactionGetResult.FromQueryGet(doc.Collection, doc.Id, firstResult);
                return getResult;
            }
            catch (TransactionOperationFailedException)
            {
                // If err is TransactionOperationFailed: propagate err.
                throw;
            }
            catch (Exception err)
            {
                var builder = ErrorBuilder.CreateError(this, err.Classify(), err);
                if (err is DocumentNotFoundException || err is CasMismatchException)
                {
                    builder.RetryTransaction();
                }

                var toThrow = builder.Build();
                SaveErrorWrapper(toThrow);
                throw toThrow;
            }
        }

        private static JObject TxDataForReplaceAndRemove(TransactionGetResult doc)
        {
            var txdata = new JObject(
                new JProperty("kv", true),
                new JProperty("scas", doc.Cas.ToString(CultureInfo.InvariantCulture)));
            if (doc.TxnMeta != null)
            {
                txdata.Add(new JProperty("txnMeta", doc.TxnMeta));
            }

            return txdata;
        }

        private async Task SetAtrPendingIfFirstMutation(ICouchbaseCollection collection, IRequestSpan? parentSpan)
        {
            if (_stagedMutations.IsEmpty)
            {
                await SetAtrPending(parentSpan).CAF();
            }
        }

        private async Task<TransactionGetResult> CreateStagedReplace(TransactionGetResult doc, object content, bool accessDeleted, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            _ = _atr ?? throw new ArgumentNullException(nameof(_atr), "ATR should have already been initialized");
            try
            {
                try
                {
                    _testHooks.Sync(HookPoint.BeforeStagedReplace, this, doc.Id);
                    var contentWrapper = new JObjectContentWrapper(content);
                    bool isTombstone = doc.Cas == 0;
                    (var updatedCas, var mutationToken) = await _docs.MutateStagedReplace(doc, content, _atr, accessDeleted).CAF();
                    Logger.LogDebug("{method} for {redactedId}, attemptId={attemptId}, preCase={preCas}, postCas={postCas}, accessDeleted={accessDeleted}", nameof(CreateStagedReplace), Redactor.UserData(doc.Id), AttemptId, doc.Cas, updatedCas, accessDeleted);
                    _testHooks.Sync(HookPoint.AfterStagedReplaceComplete, this, doc.Id);

                    doc.Cas = updatedCas;

                    var stagedOld = _stagedMutations.Find(doc);
                    if (stagedOld != null)
                    {
                        _stagedMutations.Remove(stagedOld);
                    }

                    if (stagedOld?.Type == StagedMutationType.Insert)
                    {
                        // If doc is already in stagedMutations as an INSERT or INSERT_SHADOW, then remove that, and add this op as a new INSERT or INSERT_SHADOW(depending on what was replaced).
                        _stagedMutations.Add(new StagedMutation(doc, content, StagedMutationType.Insert, mutationToken));
                    }
                    else
                    {
                        // If doc is already in stagedMutations as a REPLACE, then overwrite it.
                        _stagedMutations.Add(new StagedMutation(doc, content, StagedMutationType.Replace, mutationToken));
                    }

                    return TransactionGetResult.FromInsert(
                        doc.Collection,
                        doc.Id,
                        contentWrapper,
                        _overallContext.TransactionId,
                        AttemptId,
                        _atr.AtrId,
                        _atr.BucketName,
                        _atr.ScopeName,
                        _atr.CollectionName,
                        updatedCas,
                        isTombstone);
                }
                catch (Exception ex)
                {
                    var triaged = _triage.TriageCreateStagedRemoveOrReplaceError(ex);
                    if (triaged.ec == ErrorClass.FailExpiry)
                    {
                        _expirationOvertimeMode = true;
                    }

                    throw _triage.AssertNotNull(triaged, ex);
                }
            }
            catch (TransactionOperationFailedException toSave)
            {
                SaveErrorWrapper(toSave);
                throw;
            }
        }

        /// <summary>
        /// Insert a document.
        /// </summary>
        /// <param name="collection">The collection to insert the document into.</param>
        /// <param name="id">The ID of the new document.</param>
        /// <param name="content">The content of the new document.</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="TransactionGetResult"/> representing the inserted document.</returns>
        public Task<TransactionGetResult> InsertAsync(ICouchbaseCollection collection, string id, object content, IRequestSpan? parentSpan = null)
            => _queryMode ? InsertWithQuery(collection, id, content, parentSpan) : InsertWithKv(collection, id, content, parentSpan);

        private async Task<TransactionGetResult> InsertWithKv(ICouchbaseCollection collection, string id, object content, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            using var logScope = Logger.BeginMethodScope();
            DoneCheck();
            CheckErrors();

            var stagedOld = _stagedMutations.Find(collection, id);
            if (stagedOld?.Type == StagedMutationType.Insert || stagedOld?.Type == StagedMutationType.Replace)
            {
                throw new DocumentExistsException();
            }

            CheckExpiryAndThrow(id, hookPoint: DefaultTestHooks.HOOK_INSERT);

            await InitAtrIfNeeded(collection, id, traceSpan.Item).CAF();
            await SetAtrPendingIfFirstMutation(collection, traceSpan.Item).CAF();

            if (stagedOld?.Type == StagedMutationType.Remove)
            {
                return await CreateStagedReplace(stagedOld.Doc, content, true, traceSpan.Item).CAF();
            }

            return await CreateStagedInsert(collection, id, content, parentSpan: traceSpan.Item).CAF();
        }

        private async Task<TransactionGetResult> InsertWithQuery(ICouchbaseCollection collection, string id, object content, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            try
            {
                var queryOptions = NonStreamingQuery().Parameter(collection.MakeKeyspace())
                                               .Parameter(id)
                                               .Parameter(content)
                                               .Parameter(new { });
                using var queryResult = await QueryWrapper<QueryInsertResult>(0, _queryContextScope, "EXECUTE __insert",
                    options: queryOptions,
                    hookPoint: DefaultTestHooks.HOOK_QUERY_KV_INSERT,
                    txdata: JObject.FromObject(new { kv = true }),
                    parentSpan: traceSpan.Item).CAF();

                var firstResult = await queryResult.FirstOrDefaultAsync().CAF();
                if (firstResult == null)
                {
                    throw new DocumentNotFoundException();
                }

                var getResult = TransactionGetResult.FromQueryInsert(collection, id, content, firstResult);
                return getResult;

            }
            catch (Exception err)
            {
                if (err is TransactionOperationFailedException)
                {
                    throw;
                }

                if (err is DocumentExistsException)
                {
                    throw;
                }

                var builder = ErrorBuilder.CreateError(this, err.Classify(), err);
                var toThrow = builder.Build();
                SaveErrorWrapper(toThrow);
                throw toThrow;
            }
        }

        private async Task<TransactionGetResult> CreateStagedInsert(ICouchbaseCollection collection, string id, object content, ulong? cas = null, IRequestSpan? parentSpan = null)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            try
            {
                bool isTombstone = cas == null;
                var result = await RepeatUntilSuccessOrThrow<TransactionGetResult?>(async () =>
                {
                    try
                    {
                        // Check expiration again, since insert might be retried.
                        ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_CREATE_STAGED_INSERT, id);

                        _testHooks.Sync(HookPoint.BeforeStagedInsert, this, id);
                        var contentWrapper = new JObjectContentWrapper(content);
                        (var updatedCas, var mutationToken) = await _docs.MutateStagedInsert(collection, id, content, _atr!, cas).CAF();
                        Logger.LogDebug("{method} for {redactedId}, attemptId={attemptId}, preCas={preCas}, postCas={postCas}", nameof(CreateStagedInsert), Redactor.UserData(id), AttemptId, cas, updatedCas);
                        _ = _atr ?? throw new ArgumentNullException(nameof(_atr), "ATR should have already been initialized");
                        var getResult = TransactionGetResult.FromInsert(
                            collection,
                            id,
                            contentWrapper,
                            _overallContext.TransactionId,
                            AttemptId,
                            _atr.AtrId,
                            _atr.BucketName,
                            _atr.ScopeName,
                            _atr.CollectionName,
                            updatedCas,
                            isTombstone);

                        _testHooks.Sync(HookPoint.AfterStagedInsertComplete, this, id);

                        var stagedMutation = new StagedMutation(getResult, content, StagedMutationType.Insert,
                            mutationToken);
                        _stagedMutations.Add(stagedMutation);

                        return (RepeatAction.NoRepeat, getResult);
                    }
                    catch (Exception ex)
                    {
                        var triaged = _triage.TriageCreateStagedInsertErrors(ex, _expirationOvertimeMode);
                        switch (triaged.ec)
                        {
                            case ErrorClass.FailExpiry:
                                _expirationOvertimeMode = true;
                                throw _triage.AssertNotNull(triaged, ex);
                            case ErrorClass.FailAmbiguous:
                                return (RepeatAction.RepeatWithDelay, null);
                            case ErrorClass.FailCasMismatch:
                            case ErrorClass.FailDocAlreadyExists:
                                TransactionGetResult? docAlreadyExistsResult = null;
                                var repeatAction = await RepeatUntilSuccessOrThrow<RepeatAction>(async () =>
                                {
                                    try
                                    {
                                        Logger.LogDebug("{method}.HandleDocExists for {redactedId}, attemptId={attemptId}, preCas={preCas}", nameof(CreateStagedInsert), Redactor.UserData(id), AttemptId, 0);
                                        _testHooks.Sync(HookPoint.BeforeGetDocInExistsDuringStagedInsert, this, id);
                                        var docWithMeta = await _docs.LookupDocumentAsync(collection, id, fullDocument: false).CAF();
                                        await ForwardCompatibility.Check(this, ForwardCompatibility.WriteWriteConflictInsertingGet, docWithMeta?.TransactionXattrs?.ForwardCompatibility).CAF();

                                        var docInATransaction =
                                            docWithMeta?.TransactionXattrs?.Id?.Transactionid != null;
                                        isTombstone = docWithMeta?.IsDeleted == true;

                                        if (isTombstone && !docInATransaction)
                                        {
                                            // If the doc is a tombstone and not in any transaction
                                            // -> It’s ok to go ahead and overwrite.
                                            // Perform this algorithm (createStagedInsert) from the top with cas=the cas from the get.
                                            cas = docWithMeta!.Cas;

                                            // (innerRepeat, createStagedInsertRepeat)
                                            return (RepeatAction.NoRepeat, RepeatAction.RepeatNoDelay);
                                        }

                                        //Old Behaviour:
                                        // Else if the doc is not in a transaction
                                        // -> Raise Error(FAIL_DOC_ALREADY_EXISTS, cause=DocumentExistsException).
                                        // There is logic further up the stack that handles this by fast-failing the transaction.

                                        //From ExtInsertExisting (TXNN-131)
                                        //Fail with DocumentExistsException so users can choose to ignore and continue with the transaction
                                        //is desired.
                                        if (!docInATransaction)
                                        {
                                            throw ex;
                                        }
                                        else
                                        {
                                            // TODO: BF-CBD-3787
                                            var operationType = docWithMeta?.TransactionXattrs?.Operation?.Type;
                                            if (operationType != "insert")
                                            {
                                                Logger.LogWarning("BF-CBD-3787 FAIL_DOC_ALREADY_EXISTS here because type = {operationType}", operationType);
                                                throw ErrorBuilder.CreateError(this, ErrorClass.FailDocAlreadyExists, new DocumentExistsException()).Build();
                                            }

                                            // Else call the CheckWriteWriteConflict logic, which conveniently does everything we need to handle the above cases.
                                            var getResult = docWithMeta!.GetPostTransactionResult();
                                            await CheckWriteWriteConflict(getResult, ForwardCompatibility.WriteWriteConflictInserting, traceSpan.Item).CAF();

                                            // BF-CBD-3787: If the document is a staged insert but also is not a tombstone (e.g. it is from protocol 1.0), it must be deleted first
                                            if (operationType == "insert" && !isTombstone)
                                            {
                                                try
                                                {
                                                    await _docs.UnstageRemove(collection, id, getResult.Cas).CAF();
                                                }
                                                catch (Exception err)
                                                {
                                                    var ec = err.Classify();
                                                    switch (ec)
                                                    {
                                                        case ErrorClass.FailDocNotFound:
                                                        case ErrorClass.FailCasMismatch:
                                                            throw ErrorBuilder.CreateError(this, ec, err).RetryTransaction().Build();
                                                        default:
                                                            throw ErrorBuilder.CreateError(this, ec, err).Build();
                                                    }
                                                }

                                                // hack workaround for NCBC-2944
                                                // Supposed to "retry this (CreateStagedInsert) algorithm with the cas from the Remove", but we don't have a Cas from the Remove.
                                                // Instead, we just trigger a retry of the entire transaction, since this is such an edge case.
                                                throw ErrorBuilder.CreateError(this, ErrorClass.FailDocAlreadyExists, ex).RetryTransaction().Build();
                                            }

                                            // If this logic succeeds, we are ok to overwrite the doc.
                                            // Perform this algorithm (createStagedInsert) from the top, with cas=the cas from the get.
                                            cas = docWithMeta.Cas;
                                            return (RepeatAction.NoRepeat, RepeatAction.RepeatNoDelay);
                                        }
                                    }
                                    catch (Exception exDocExists)
                                    {
                                        if (exDocExists is DocumentExistsException) throw;
                                        var triagedDocExists = _triage.TriageDocExistsOnStagedInsertErrors(exDocExists);
                                        throw _triage.AssertNotNull(triagedDocExists, exDocExists);
                                    }
                                }).CAF();

                                return (repeatAction, docAlreadyExistsResult);
                        }

                        throw _triage.AssertNotNull(triaged, ex);
                    }
                }).CAF();

                return result ?? throw new InvalidOperationException("Final result should not be null");
            }
            catch (TransactionOperationFailedException toSave)
            {
                SaveErrorWrapper(toSave);
                throw;
            }
        }

        private async Task SetAtrPending(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            var atrId = _atr!.AtrId;
            try
            {
                await RepeatUntilSuccessOrThrow(async () =>
                {
                    try
                    {
                        var docDurability = _effectiveDurabilityLevel;
                        ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ATR_PENDING);
                        _testHooks.Sync(HookPoint.BeforeAtrPending, this);
                        var t1 = _overallContext.StartTime;
                        var t2 = DateTimeOffset.UtcNow;
                        var tElapsed = t2 - t1;
                        var tc = _config.Timeout;
                        var tRemaining = tc - tElapsed;
                        var exp = (ulong)Math.Max(Math.Min(tRemaining.TotalMilliseconds, tc.TotalMilliseconds), 0);
                        await _atr.MutateAtrPending(exp, docDurability).CAF();
                        Logger?.LogDebug($"{nameof(SetAtrPending)} for {Redactor.UserData(_atr.FullPath)} (attempt={AttemptId})");
                        _testHooks.Sync(HookPoint.AfterAtrPending, this);
                        _state = AttemptStates.PENDING;
                        return RepeatAction.NoRepeat;
                    }
                    catch (Exception ex)
                    {
                        var triaged = _triage.TriageSetAtrPendingErrors(ex, _expirationOvertimeMode);
                        Logger.LogWarning("Failed with {ec} in {method}: {reason}", triaged.ec, nameof(SetAtrPending), ex.Message);
                        switch (triaged.ec)
                        {
                            case ErrorClass.FailExpiry:
                                _expirationOvertimeMode = true;
                                break;
                            case ErrorClass.FailAmbiguous:
                                return RepeatAction.RepeatWithDelay;
                            case ErrorClass.FailPathAlreadyExists:
                                // proceed as though op was successful.
                                return RepeatAction.NoRepeat;
                        }

                        throw _triage.AssertNotNull(triaged, ex);
                    }
                }).CAF();
            }
            catch (TransactionOperationFailedException toSave)
            {
                SaveErrorWrapper(toSave);
                throw;
            }
        }

        /// <summary>
        /// Remove a document previously looked up in this transaction.
        /// </summary>
        /// <param name="doc">The <see cref="TransactionGetResult"/> of a document previously looked up in this transaction.</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A task representing the asynchronous work.</returns>
        public Task RemoveAsync(TransactionGetResult doc, IRequestSpan? parentSpan = null)
            => _queryMode ? RemoveWithQuery(doc, parentSpan) : RemoveWithKv(doc, parentSpan);

        private async Task RemoveWithKv(TransactionGetResult doc, IRequestSpan? parentSpan = null)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            DoneCheck();
            CheckErrors();
            CheckExpiryAndThrow(doc.Id, DefaultTestHooks.HOOK_REMOVE);

            var stagedOld = _stagedMutations.Find(doc);
            if (stagedOld != null)
            {
                if (stagedOld != null)
                {
                    if (stagedOld.Type == StagedMutationType.Remove)
                    {
                        throw ErrorBuilder.CreateError(this, ErrorClass.FailDocNotFound, new DocumentNotFoundException()).Build();
                    }
                    else if (stagedOld.Type == StagedMutationType.Insert)
                    {
                        try
                        {
                            await RemoveStagedInsert(doc, traceSpan.Item).CAF();
                        }
                        catch (TransactionOperationFailedException err)
                        {
                            SaveErrorWrapper(err);
                            throw;
                        }

                        return;
                    }
                }
            }

            await CheckWriteWriteConflict(doc, ForwardCompatibility.WriteWriteConflictRemoving, traceSpan.Item).CAF();
            await InitAtrIfNeeded(doc.Collection, doc.Id, traceSpan.Item).CAF();
            await SetAtrPendingIfFirstMutation(doc.Collection, traceSpan.Item).CAF();
            await CreateStagedRemove(doc, traceSpan.Item).CAF();
        }

        private async Task RemoveWithQuery(TransactionGetResult doc, IRequestSpan? parentSpan)
        {
            _ = doc ?? throw new ArgumentNullException(nameof(doc));
            using var traceSpan = TraceSpan(parent: parentSpan);
            JObject txdata = TxDataForReplaceAndRemove(doc);

            try
            {
                var queryOptions = NonStreamingQuery().Parameter(doc.Collection.MakeKeyspace())
                                                      .Parameter(doc.Id)
                                                      .Parameter(new { });
                using var queryResult = await QueryWrapper<QueryGetResult>(0, _queryContextScope, "EXECUTE __delete",
                    options: queryOptions,
                    hookPoint: DefaultTestHooks.HOOK_QUERY_KV_REMOVE,
                    txdata: txdata,
                    parentSpan: traceSpan.Item).CAF();

                _ = await queryResult.FirstOrDefaultAsync().CAF();
            }
            catch (TransactionOperationFailedException)
            {
                // If err is TransactionOperationFailed: propagate err.
                throw;
            }
            catch (Exception err)
            {
                var builder = ErrorBuilder.CreateError(this, err.Classify(), err);
                if (err is DocumentNotFoundException || err is CasMismatchException)
                {
                    builder.RetryTransaction();
                }

                var toThrow = builder.Build();
                SaveErrorWrapper(toThrow);
                throw toThrow;
            }
        }

        private async Task CreateStagedRemove(TransactionGetResult doc, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            try
            {
                try
                {
                    _testHooks.Sync(HookPoint.BeforeStagedRemove, this, doc.Id);
                    (var updatedCas, var mutationToken) = await _docs.MutateStagedRemove(doc, _atr!).CAF();
                    Logger?.LogDebug($"{nameof(CreateStagedRemove)} for {Redactor.UserData(doc.Id)}, attemptId={AttemptId}, preCas={doc.Cas}, postCas={updatedCas}");
                    _testHooks.Sync(HookPoint.AfterStagedRemoveComplete, this, doc.Id);

                    doc.Cas = updatedCas;


                    var stagedRemove = new StagedMutation(doc, null,
                        StagedMutationType.Remove, mutationToken);
                    _stagedMutations.Add(stagedRemove);
                }
                catch (Exception ex)
                {
                    var triaged = _triage.TriageCreateStagedRemoveOrReplaceError(ex);
                    if (triaged.ec == ErrorClass.FailExpiry)
                    {
                        _expirationOvertimeMode = true;
                    }

                    throw _triage.AssertNotNull(triaged, ex);
                }
            }
            catch (TransactionOperationFailedException toSave)
            {
                SaveErrorWrapper(toSave);
                throw;
            }
        }

        private async Task RemoveStagedInsert(TransactionGetResult doc, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            if (HasExpiredClientSide(doc.Id, "removeStagedInsert"))
            {
                throw ErrorBuilder.CreateError(this, ErrorClass.FailExpiry)
                    .Cause(new AttemptExpiredException(this, "Expired in 'removeStagedInsert'"))
                    .DoNotRollbackAttempt()
                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                    .Build();
            }

            try
            {
                _testHooks.Sync(HookPoint.BeforeRemoveStagedInsert, this, doc.Id);
                (var removedCas, _) = await _docs.RemoveStagedInsert(doc).CAF();
                _testHooks.Sync(HookPoint.AfterStagedRemoveComplete, this, doc.Id);
                doc.Cas = removedCas;
                _stagedMutations.Remove(doc);
            }
            catch (Exception err)
            {
                var ec = err.Classify();
                if (ec == ErrorClass.TransactionOperationFailed)
                {
                    throw;
                }

                if (ec == ErrorClass.FailHard)
                {
                    throw ErrorBuilder.CreateError(this, ec, err).DoNotRollbackAttempt().Build();
                }

                throw ErrorBuilder.CreateError(this, ec, err).RetryTransaction().Build();
            }
        }

        internal async Task AutoCommit(IRequestSpan? parentSpan)
        {
            if (IsDone)
            {
                return;
            }

            switch (_state)
            {
                case AttemptStates.NOTHING_WRITTEN:
                case AttemptStates.PENDING:
                    await CommitAsync(parentSpan).CAF();
                    break;
            }
        }

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        /// <param name="parentSpan">(optional) RequestSpan to use as a parent for tracing.</param>
        public async Task CommitAsync(IRequestSpan? parentSpan = null)
        {
            if (!_previousErrors.IsEmpty)
            {
                _triage.ThrowIfCommitWithPreviousErrors(_previousErrors.Values);
            }

            if (_queryMode)
            {
                await CommitWithQuery(parentSpan).CAF();
            }
            else
            {
                await CommitWithKv(parentSpan).CAF();
            }
        }

        private async Task CommitWithKv(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#CommitAsync
            CheckExpiryAndThrow(null, DefaultTestHooks.HOOK_BEFORE_COMMIT);
            DoneCheck();
            IsDone = true;

            if (_atr?.AtrId == null || _atr?.Collection == null)
            {
                // If no mutation has been performed. Return success.
                // This will leave state as NOTHING_WRITTEN,
                Logger.LogInformation("Nothing to commit.");
                return;
            }

            await SetAtrCommit(traceSpan.Item).CAF();
            await UnstageDocs(traceSpan.Item).CAF();
            await SetAtrComplete(traceSpan.Item).CAF();
        }

        private async Task CommitWithQuery(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            try
            {
                await QueryWrapper<object>(
                    statementId: 0,
                    scope: _queryContextScope,
                    statement: "COMMIT",
                    options: new QueryOptions(),
                    hookPoint: DefaultTestHooks.HOOK_QUERY_COMMIT,
                    parentSpan: traceSpan.Item).CAF();
                _state = AttemptStates.COMPLETED;
                UnstagingComplete = true;
            }
            catch (TransactionOperationFailedException)
            {
                throw;
            }
            catch (Exception err)
            {
                var ec = err.Classify();
                if (ec == ErrorClass.FailExpiry)
                {
                    throw ErrorBuilder.CreateError(this, ec, err)
                        .RaiseException(TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous)
                        .DoNotRollbackAttempt()
                        .Build();
                }

                throw ErrorBuilder.CreateError(this, ec, err).DoNotRollbackAttempt().Build();
            }
            finally
            {
                IsDone = true;
            }
        }

        private async Task SetAtrComplete(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#SetATRComplete
            if (HasExpiredClientSide(null, DefaultTestHooks.HOOK_ATR_COMPLETE) && !_expirationOvertimeMode)
            {
                // If transaction has expired and not in ExpiryOvertimeMode: though technically expired, the transaction should be regarded
                // as successful, as this is just a cleanup step.
                // Return success.
                return;
            }

            try
            {
                _testHooks.Sync(HookPoint.BeforeAtrComplete, this);
                await _atr!.MutateAtrComplete().CAF();
                Logger?.LogDebug($"{nameof(SetAtrComplete)} for {Redactor.UserData(_atr.FullPath)} (attempt={AttemptId})");
                _testHooks.Sync(HookPoint.AfterAtrComplete, this);
                _state = AttemptStates.COMPLETED;
                UnstagingComplete = true;
            }
            catch (Exception ex)
            {
                var triaged = _triage.TriageSetAtrCompleteErrors(ex);
                if (triaged.toThrow != null)
                {
                    throw triaged.toThrow;
                }
                else
                {
                    // Else -> Setting the ATR to COMPLETED is purely a cleanup step, there’s no need to retry it until expiry.
                    // Simply return success (leaving state at COMMITTED).
                    return;
                }
            }
        }

        private async Task UnstageDocs(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            var allStagedMutations = _stagedMutations.ToList();
            foreach (var sm in allStagedMutations)
            {
                (var cas, var content) = await FetchIfNeededBeforeUnstage(sm).CAF();
                switch (sm.Type)
                {
                    case StagedMutationType.Remove:
                        await UnstageRemove(sm).CAF();
                        break;
                    case StagedMutationType.Insert:
                        if (content is null)
                        {
                            throw new InvalidOperationException("Cannot unstage Insert with no content.");
                        }
                        await UnstageInsertOrReplace(sm, cas, content, insertMode: true, ambiguityResolutionMode: false).CAF();
                        break;
                    case StagedMutationType.Replace:
                        if (content is null)
                        {
                            throw new InvalidOperationException("Cannot unstage Replace with no content.");
                        }
                        await UnstageInsertOrReplace(sm, cas, content, insertMode: false, ambiguityResolutionMode: false).CAF();
                        break;
                    default:
                        throw new InvalidOperationException($"Cannot unstage transaction mutation of type {sm.Type}");
                }
            }
        }

        private async Task UnstageRemove(StagedMutation sm, bool ambiguityResolutionMode = false, IRequestSpan? parentSpan = null)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Unstaging-Removes
            int retryCount = -1;
            await RepeatUntilSuccessOrThrow(async () =>
            {
                retryCount++;
                try
                {
                    _testHooks.Sync(HookPoint.BeforeDocRemoved, this, sm.Doc.Id);
                    if (!_expirationOvertimeMode && HasExpiredClientSide(sm.Doc.Id, DefaultTestHooks.HOOK_REMOVE_DOC))
                    {
                        _expirationOvertimeMode = true;
                    }

                    await _docs.UnstageRemove(sm.Doc.Collection, sm.Doc.Id).CAF();
                    Logger.LogDebug("Unstaged RemoveAsync successfully for {redactedId)} (retryCount={retryCount}", Redactor.UserData(sm.Doc.FullyQualifiedId), retryCount);
                    _testHooks.Sync(HookPoint.AfterDocRemovedPreRetry, this, sm.Doc.Id);

                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    var triaged = _triage.TriageUnstageRemoveErrors(ex, _expirationOvertimeMode);
                    if (_expirationOvertimeMode)
                    {
                        throw ErrorBuilder.CreateError(this, ErrorClass.FailExpiry, new AttemptExpiredException(this))
                            .DoNotRollbackAttempt()
                            .RaiseException(TransactionOperationFailedException.FinalError.TransactionFailedPostCommit)
                            .Build();
                    }

                    switch (triaged.ec)
                    {
                        case ErrorClass.FailAmbiguous:
                            ambiguityResolutionMode = true;
                            return RepeatAction.RepeatWithDelay;
                    }

                    throw _triage.AssertNotNull(triaged, ex);
                }
            }).CAF();

            _finalMutations.Add(sm.MutationToken);
            _testHooks.Sync(HookPoint.AfterDocRemovedPostRetry, this, sm.Doc.Id);
        }

        private Task<(ulong cas, object? content)> FetchIfNeededBeforeUnstage(StagedMutation sm)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#FetchIfNeededBeforeUnstage
            // TODO: consider implementing ExtMemoryOptUnstaging mode
            // For now, assuming ExtTimeOptUnstaging mode...
            return Task.FromResult((sm.Doc.Cas, sm.Content));
        }

        private async Task UnstageInsertOrReplace(StagedMutation sm, ulong cas, object content, bool insertMode = false, bool ambiguityResolutionMode = false, IRequestSpan? parentSpan = null)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Unstaging-Inserts-and-Replaces-Protocol-20-version

            await RepeatUntilSuccessOrThrow(async () =>
            {
                try
                {
                    if (!_expirationOvertimeMode && HasExpiredClientSide(sm.Doc.Id, DefaultTestHooks.HOOK_COMMIT_DOC))
                    {
                        _expirationOvertimeMode = true;
                    }

                    _testHooks.Sync(HookPoint.BeforeDocCommitted, this, sm.Doc.Id);
                    (ulong updatedCas, MutationToken? mutationToken) = await _docs.UnstageInsertOrReplace(sm.Doc.Collection, sm.Doc.Id, cas, content, insertMode).CAF();
                    Logger.LogInformation(
                        "Unstaged mutation successfully on {redactedId}, attempt={attemptId}, insertMode={insertMode}, ambiguityResolutionMode={ambiguityResolutionMode}, preCas={cas}, postCas={updatedCas}",
                        Redactor.UserData(sm.Doc.FullyQualifiedId),
                        AttemptId,
                        insertMode,
                        ambiguityResolutionMode,
                        cas,
                        updatedCas);

                    if (mutationToken != null)
                    {
                        _finalMutations.Add(mutationToken);
                    }

                    _testHooks.Sync(HookPoint.AfterDocCommittedBeforeSavingCas, this, sm.Doc.Id);

                    sm.Doc.Cas = updatedCas;
                    _testHooks.Sync(HookPoint.AfterDocCommitted, this, sm.Doc.Id);

                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    var triaged = _triage.TriageUnstageInsertOrReplaceErrors(ex, _expirationOvertimeMode);
                    if (_expirationOvertimeMode)
                    {
                        throw ErrorBuilder.CreateError(this, ErrorClass.FailExpiry, new AttemptExpiredException(this))
                            .DoNotRollbackAttempt()
                            .RaiseException(TransactionOperationFailedException.FinalError.TransactionFailedPostCommit)
                            .Build();
                    }

                    switch (triaged.ec)
                    {
                        case ErrorClass.FailAmbiguous:
                            ambiguityResolutionMode = true;
                            return RepeatAction.RepeatWithDelay;
                        case ErrorClass.FailCasMismatch:
                            if (ambiguityResolutionMode)
                            {
                                throw _triage.AssertNotNull(triaged, ex);
                            }
                            else
                            {
                                cas = 0;
                                return RepeatAction.RepeatWithDelay;
                            }
                        case ErrorClass.FailDocNotFound:
                            // TODO: publish IllegalDocumentState event to the application.
                            Logger?.LogError("IllegalDocumentState: " + triaged.ec);
                            insertMode = true;
                            return RepeatAction.RepeatWithDelay;
                        case ErrorClass.FailDocAlreadyExists:
                            if (ambiguityResolutionMode)
                            {
                                throw _triage.AssertNotNull(triaged, ex);
                            }
                            else
                            {
                                // TODO: publish an IllegalDocumentState event to the application.
                                Logger?.LogError("IllegalDocumentState: " + triaged.ec);
                                insertMode = false;
                                cas = 0;
                                return RepeatAction.RepeatWithDelay;
                            }
                    }

                    throw _triage.AssertNotNull(triaged, ex);
                }
            }).CAF();
        }

        private async Task SetAtrCommit(IRequestSpan? parentSpan)
        {
            _ = _atr ?? throw new InvalidOperationException($"{nameof(SetAtrCommit)} without initializing ATR.");
            var ambiguityResolutionMode = false;
            using var traceSpan = TraceSpan(parent: parentSpan);
            await RepeatUntilSuccessOrThrow(async () =>
            {
                if (ambiguityResolutionMode)
                {
                    Logger.LogDebug("Retrying SetAtrCommit with ambiguity resolution");
                }

                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ATR_COMMIT);
                    _testHooks.Sync(HookPoint.BeforeAtrCommit, this);
                    await _atr.MutateAtrCommit(_stagedMutations.ToList()).CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})", nameof(SetAtrCommit), Redactor.UserData(_atr.FullPath), AttemptId);
                    _testHooks.Sync(HookPoint.AfterAtrCommit);
                    _state = AttemptStates.COMMITTED;
                    return RepeatAction.NoRepeat;
                }
                catch (Exception err)
                {
                    var ec = err.Classify();
                    Logger.LogWarning("Failed attempt at committing due to {ec}", ec);
                    if (ec == ErrorClass.FailExpiry)
                    {
                        if (ambiguityResolutionMode)
                        {
                            throw Error(ec, new AttemptExpiredException(this, "Attempt expired ambiguously in " + nameof(SetAtrCommit)), rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        }
                        else
                        {
                            throw Error(ec, new AttemptExpiredException(this, "Attempt expired in " + nameof(SetAtrCommit)), rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionExpired);
                        }
                    }
                    else if (ec == ErrorClass.FailAmbiguous)
                    {
                        ambiguityResolutionMode = true;
                        return RepeatAction.RepeatWithDelay;
                    }
                    else if (ec == ErrorClass.FailHard)
                    {
                        if (ambiguityResolutionMode)
                        {
                            throw Error(ec, err, rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        }

                        throw Error(ec, err, rollback: false);
                    }
                    else if (ec == ErrorClass.FailTransient)
                    {
                        if (ambiguityResolutionMode)
                        {
                            // We haven't yet reached clarity on what state this attempt is in, so we can’t rollback or continue.
                            return RepeatAction.RepeatWithDelay;
                        }

                        throw Error(ec, err, retry: true);
                    }
                    else if (ec == ErrorClass.FailPathAlreadyExists)
                    {
                        var repeatAction = await ResolveSetAtrCommitAmbiguity(traceSpan.Item).CAF();
                        if (repeatAction != RepeatAction.NoRepeat)
                        {
                            ambiguityResolutionMode = false;
                        }

                        return repeatAction;
                    }
                    else
                    {
                        var cause = err;
                        var rollback = true;
                        if (ec == ErrorClass.FailDocNotFound)
                        {
                            cause = new ActiveTransactionRecordNotFoundException();
                            rollback = false;
                        }
                        else if (ec == ErrorClass.FailPathNotFound)
                        {
                            cause = new ActiveTransactionRecordEntryNotFoundException();
                            rollback = false;
                        }
                        else if (ec == ErrorClass.FailAtrFull)
                        {
                            cause = new ActiveTransactionRecordsFullException(this, "Full ATR in SetAtrCommit");
                            rollback = false;
                        }

                        if (ambiguityResolutionMode == true)
                        {
                            // we were unable to attain clarity
                            throw Error(ec, cause, rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        }

                        throw Error(ec, cause, rollback: rollback);
                    }
                }
            }).CAF();
        }

        private async Task<RepeatAction> ResolveSetAtrCommitAmbiguity(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            var setAtrCommitRetryAction = await RepeatUntilSuccessOrThrow<RepeatAction>(async () =>
            {
                string? refreshedStatus = null;
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ATR_COMMIT_AMBIGUITY_RESOLUTION);
                    _testHooks.Sync(HookPoint.BeforeAtrCommitAmbiguityResolution, this);
                    refreshedStatus = await _atr!.LookupAtrState().CAF();

                }
                catch (Exception exAmbiguity)
                {
                    var ec = exAmbiguity.Classify();
                    switch (ec)
                    {
                        case ErrorClass.FailExpiry:
                            throw Error(ec, new AttemptExpiredException(this, "expired resolving commit ambiguity", exAmbiguity), rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        case ErrorClass.FailHard:
                            throw Error(ec, exAmbiguity, rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        case ErrorClass.FailTransient:
                        case ErrorClass.FailOther:
                            return (retry: RepeatAction.RepeatWithDelay, finalVal: RepeatAction.RepeatWithDelay);
                        default:
                            var cause = exAmbiguity;
                            if (ec == ErrorClass.FailDocNotFound)
                            {
                                cause = new ActiveTransactionRecordNotFoundException();
                            }
                            else if (ec == ErrorClass.FailPathNotFound)
                            {
                                cause = new ActiveTransactionRecordEntryNotFoundException();
                            }

                            throw Error(ec, cause, rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                    }
                }

                if (!Enum.TryParse<AttemptStates>(refreshedStatus, out var parsedRefreshStatus))
                {
                    throw ErrorBuilder.CreateError(this, ErrorClass.FailOther)
                        .Cause(new InvalidOperationException(
                            $"ATR state '{refreshedStatus}' could not be parsed"))
                        .DoNotRollbackAttempt()
                        .Build();
                }

                switch (parsedRefreshStatus)
                {
                    case AttemptStates.COMMITTED:
                        // The ambiguous operation actually succeeded. Return success.
                        return (retry: RepeatAction.NoRepeat, finalVal: RepeatAction.NoRepeat);
                    case AttemptStates.ABORTED:
                        throw ErrorBuilder.CreateError(this, ErrorClass.FailOther).RetryTransaction().Build();
                    default:
                        // Unknown status, perhaps from a future protocol or extension.
                        // Bailout and leave the transaction for cleanup by raising
                        // Error(ec = FAIL_OTHER, rollback=false, cause=IllegalStateException
                        throw Error(ErrorClass.FailOther, new InvalidOperationException("Unknown ATR state: " + refreshedStatus), rollback: false);
                }
            }).CAF();

            return setAtrCommitRetryAction;
        }

        private async Task SetAtrAborted(bool isAppRollback, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            Logger.LogInformation("Setting Aborted status.  isAppRollback={isAppRollback}", isAppRollback);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#SetATRAborted
            await RepeatUntilSuccessOrThrow(async () =>
            {
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ATR_ABORT);
                    _testHooks.Sync(HookPoint.BeforeAtrAborted, this);
                    await _atr!.MutateAtrAborted(_stagedMutations.ToList()).CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})", nameof(SetAtrAborted), Redactor.UserData(_atr.FullPath), AttemptId);
                    _testHooks.Sync(HookPoint.AfterAtrAborted, this);
                    _state = AttemptStates.ABORTED;
                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    if (_expirationOvertimeMode)
                    {
                        throw ErrorBuilder.CreateError(this, ErrorClass.FailExpiry)
                            .Cause(new AttemptExpiredException(this, "Expired in " + nameof(SetAtrAborted)))
                            .DoNotRollbackAttempt()
                            .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                            .Build();
                    }

                    (ErrorClass ec, TransactionOperationFailedException? toThrow) = _triage.TriageSetAtrAbortedErrors(ex);
                    switch (ec)
                    {
                        case ErrorClass.FailExpiry:
                            _expirationOvertimeMode = true;
                            return RepeatAction.RepeatWithBackoff;
                        case ErrorClass.FailPathNotFound:
                        case ErrorClass.FailDocNotFound:
                        case ErrorClass.FailAtrFull:
                        case ErrorClass.FailHard:
                            throw toThrow ?? ErrorBuilder.CreateError(this, ec, new InvalidOperationException("Failed to generate proper exception wrapper", ex))
                                .Build();

                        default:
                            return RepeatAction.RepeatWithBackoff;
                    }
                }
            }).CAF();
        }

        private async Task SetAtrRolledBack(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#SetATRRolledBack
            await RepeatUntilSuccessOrThrow(async () =>
            {
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ATR_ROLLBACK_COMPLETE);
                    _testHooks.Sync(HookPoint.BeforeAtrRolledBack, this);
                    await _atr!.MutateAtrRolledBack().CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})",
                        nameof(SetAtrRolledBack),
                        Redactor.UserData(_atr.FullPath),
                        AttemptId);
                    _testHooks.Sync(HookPoint.AfterAtrRolledBack, this);
                    _state = AttemptStates.ROLLED_BACK;
                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    BailoutIfInOvertime(rollback: false);

                    (ErrorClass ec, TransactionOperationFailedException? toThrow) = _triage.TriageSetAtrRolledBackErrors(ex);
                    switch (ec)
                    {
                        case ErrorClass.FailPathNotFound:
                        case ErrorClass.FailDocNotFound:
                            // Whatever has happened, the necessary handling for all these is the same: continue as if success.
                            // The ATR entry has been removed
                            return RepeatAction.NoRepeat;
                        case ErrorClass.FailExpiry:
                        case ErrorClass.FailHard:
                            throw toThrow ?? ErrorBuilder.CreateError(this, ec,
                                    new InvalidOperationException("Failed to generate proper exception wrapper", ex))
                                .Build();
                        default:
                            return RepeatAction.RepeatWithBackoff;
                    }
                }
            }).CAF();
        }

        /// <summary>
        /// Rollback the transaction, explicitly.
        /// </summary>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A task representing the asynchronous work.</returns>
        /// <remarks>Calling this method on AttemptContext is usually unnecessary, as unhandled exceptions will trigger a rollback automatically.</remarks>
        public Task RollbackAsync(IRequestSpan? parentSpan = null) => this.RollbackInternal(true, parentSpan);

        // /// <summary>
        // /// Run a query in transaction mode.
        // /// </summary>
        // /// <typeparam name="T">The type of the result.  Use <see cref="object"/> for queries with no results.</typeparam>
        // /// <param name="statement">The statement to execute.</param>
        // /// <param name="config">The configuration to use for this query.</param>
        // /// <param name="scope">The scope</param>
        // /// <param name="parentSpan">The optional parent tracing span.</param>
        // /// <returns>A <see cref="SingleQueryTransactionResult{T}"/> with the query results, if any.</returns>
        // /// <remarks>IMPORTANT: Any KV operations after this query will be run via the query engine, which has performance implications.</remarks>
        // public Task<IQueryResult<T>> QueryAsync<T>(string statement, TransactionQueryConfigBuilder? config = null, IScope? scope = null, IRequestSpan? parentSpan = null)
        // {
        //     var options = config?.Build() ?? new TransactionQueryOptions();
        //     return QueryAsync<T>(statement, options, scope, parentSpan);
        // }

        /// <summary>
        /// Run a query in transaction mode.
        /// </summary>
        /// <typeparam name="T">The type of the result.  Use <see cref="object"/> for queries with no results.</typeparam>
        /// <param name="statement">The statement to execute.</param>
        /// <param name="options">The query options to use for this query.</param>
        /// <param name="scope">The scope</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="SingleQueryTransactionResult{T}"/> with the query results, if any.</returns>
        /// <remarks>IMPORTANT: Any KV operations after this query will be run via the query engine, which has performance implications.</remarks>
        public Task<IQueryResult<T>> QueryAsync<T>(string statement, TransactionQueryOptions options, IScope? scope = null, IRequestSpan? parentSpan = null)
            => QueryAsync<T>(statement, options, false, scope, parentSpan);

        internal async Task<IQueryResult<T>> QueryAsync<T>(string statement, TransactionQueryOptions options, bool txImplicit, IScope? scope = null, IRequestSpan? parentSpan = null)
        {
            var queryOptions = new QueryOptions();
            if (options.ScanConsistency.HasValue)
            {
                queryOptions.ScanConsistency(options.ScanConsistency.Value);
            }
            var traceSpan = TraceSpan(parent: parentSpan);
            long fixmeStatementId = 0;
            var results = await QueryWrapper<T>(
                statementId: fixmeStatementId,
                scope: scope,
                statement: statement,
                options: queryOptions,
                hookPoint: DefaultTestHooks.HOOK_QUERY,
                parentSpan: traceSpan.Item,
                txImplicit: txImplicit
                ).CAF();

            return results;
        }

        private bool IsDone { get; set; }

        internal Task RollbackInternal(bool isAppRollback, IRequestSpan? parentSpan)
            => _queryMode ? RollbackWithQuery(isAppRollback, parentSpan) : RollbackWithKv(isAppRollback, parentSpan);

        internal async Task RollbackWithKv(bool isAppRollback, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#rollbackInternal
            if (!_expirationOvertimeMode)
            {
                if (HasExpiredClientSide(null, place: DefaultTestHooks.HOOK_ROLLBACK))
                {
                    _expirationOvertimeMode = true;
                }
            }

            if (_state == AttemptStates.NOTHING_WRITTEN)
            {
                IsDone = true;
                return;
            }

            if (isAppRollback)
            {
                DoneCheck();
            }

            IsDone = true;

            await SetAtrAborted(isAppRollback, traceSpan.Item).CAF();
            var allMutations = _stagedMutations.ToList();
            foreach (var sm in allMutations)
            {
                switch (sm.Type)
                {
                    case StagedMutationType.Insert:
                        await RollbackStagedInsert(sm, traceSpan.Item).CAF();
                        break;
                    case StagedMutationType.Remove:
                    case StagedMutationType.Replace:
                        await RollbackStagedReplaceOrRemove(sm, traceSpan.Item).CAF();
                        break;
                    default:
                        throw new InvalidOperationException(sm.Type + " is not a supported mutation type for rollback.");

                }
            }

            await SetAtrRolledBack(traceSpan.Item).CAF();
        }

        internal async Task RollbackWithQuery(bool isAppRollback, IRequestSpan? parentSpan)
        {
            var traceSpan = TraceSpan(parent: parentSpan);
            try
            {
                var queryOptions = NonStreamingQuery();
                _ = await QueryWrapper<object>(0, _queryContextScope, "ROLLBACK", queryOptions,
                    hookPoint: DefaultTestHooks.HOOK_QUERY_ROLLBACK,
                    parentSpan: traceSpan.Item,
                    existingErrorCheck: false).CAF();
                _state = AttemptStates.ROLLED_BACK;
            }
            catch (Exception err)
            {
                if (err is TransactionOperationFailedException)
                {
                    throw;
                }

                if (err is AttemptNotFoundOnQueryException)
                {
                    // treat as success
                    _state = AttemptStates.ROLLED_BACK;
                }

                var toSave = ErrorBuilder.CreateError(this, err.Classify(), err)
                    .DoNotRollbackAttempt()
                    .Build();
                SaveErrorWrapper(toSave);
                throw toSave;
            }
            finally
            {
                IsDone = true;
            }
        }

        private async Task RollbackStagedInsert(StagedMutation sm, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#RollbackAsync-Staged-InsertAsync
            await RepeatUntilSuccessOrThrow(async () =>
            {
                Logger.LogDebug("[{attemptId}] rolling back staged insert for {redactedId}", AttemptId, Redactor.UserData(sm.Doc.FullyQualifiedId));
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_DELETE_INSERTED, sm.Doc.Id);
                    _testHooks.Sync(HookPoint.BeforeRollbackDeleteInserted, this, sm.Doc.Id);
                    await _docs.ClearTransactionMetadata(sm.Doc.Collection, sm.Doc.Id, sm.Doc.Cas, true).CAF();
                    Logger.LogDebug("Rolled back staged {type} for {redactedId}", sm.Type, Redactor.UserData(sm.Doc.Id));
                    _testHooks.Sync(HookPoint.AfterRollbackDeleteInserted, this, sm.Doc.Id);
                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    BailoutIfInOvertime(rollback: false);

                    (ErrorClass ec, TransactionOperationFailedException? toThrow) = _triage.TriageRollbackStagedInsertErrors(ex);
                    switch (ec)
                    {
                        case ErrorClass.FailExpiry:
                            _expirationOvertimeMode = true;
                            return RepeatAction.RepeatWithBackoff;
                        case ErrorClass.FailDocNotFound:
                        case ErrorClass.FailPathNotFound:
                            // something must have succeeded in the interim after a retry
                            return RepeatAction.NoRepeat;
                        case ErrorClass.FailCasMismatch:
                        case ErrorClass.FailHard:
                            throw _triage.AssertNotNull(toThrow, ec, ex);
                        default:
                            return RepeatAction.RepeatWithBackoff;
                    }
                }
            }).CAF();
        }

        private async Task RollbackStagedReplaceOrRemove(StagedMutation sm, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#RollbackAsync-Staged-ReplaceAsync-or-RemoveAsync
            await RepeatUntilSuccessOrThrow(async () =>
            {
                Logger.LogDebug("[{attemptId}] rolling back staged replace or remove for {redactedId}", AttemptId, Redactor.UserData(sm.Doc.FullyQualifiedId));
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ROLLBACK_DOC, sm.Doc.Id);
                    _testHooks.Sync(HookPoint.BeforeDocRolledBack, this, sm.Doc.Id);
                    await _docs.ClearTransactionMetadata(sm.Doc.Collection, sm.Doc.Id, sm.Doc.Cas, sm.Doc.IsDeleted).CAF();
                    Logger.LogDebug("Rolled back staged {type} for {redactedId}", sm.Type, Redactor.UserData(sm.Doc.Id));
                    _testHooks.Sync(HookPoint.AfterRollbackReplaceOrRemove, this, sm.Doc.Id);
                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    BailoutIfInOvertime(rollback: false);

                    var tr = _triage.TriageRollbackStagedRemoveOrReplaceErrors(ex);
                    switch (tr.ec)
                    {
                        case ErrorClass.FailExpiry:
                            _expirationOvertimeMode = true;
                            return RepeatAction.RepeatWithBackoff;
                        case ErrorClass.FailPathNotFound:
                            // must have finished elsewhere.
                            return RepeatAction.NoRepeat;
                        case ErrorClass.FailDocNotFound:
                        case ErrorClass.FailCasMismatch:
                        case ErrorClass.FailHard:
                            throw _triage.AssertNotNull(tr, ex);
                        default:
                            return RepeatAction.RepeatWithBackoff;
                    }
                }
            }).CAF();
        }

        private void DoneCheck()
        {
            var isDoneState = !(_state == AttemptStates.NOTHING_WRITTEN || _state == AttemptStates.PENDING);
            if (IsDone || isDoneState)
            {
                throw ErrorBuilder.CreateError(this, ErrorClass.FailOther)
                    .Cause(new InvalidOperationException("Cannot perform operations after a transaction has been committed or rolled back."))
                    .DoNotRollbackAttempt()
                    .Build();
            }
        }

        private void BailoutIfInOvertime(bool rollback = false, [CallerMemberName] string caller = nameof(BailoutIfInOvertime))
        {
            if (_expirationOvertimeMode)
            {
                var builder = ErrorBuilder.CreateError(this, ErrorClass.FailExpiry)
                    .Cause(new AttemptExpiredException(this, "Expired in " + nameof(caller)))
                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired);
                if (!rollback)
                {
                    builder.DoNotRollbackAttempt();
                }

                throw builder.Build();
            }
        }

        private async Task InitAtrIfNeeded(ICouchbaseCollection collection, string id, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            ICouchbaseCollection atrCollection;
            if (_config.MetadataCollection is not null)
            {
                atrCollection = await _config.MetadataCollection.GetCollectionAsync(_cluster).CAF();
            }
            else
            {
                atrCollection = collection.Scope.Bucket.DefaultCollection();
            }

            var vBucketId = AtrIds.GetVBucketId(id);
            var testHookAtrId = (string?)_testHooks.Sync(HookPoint.AtrIdForVbucket, this,
                vBucketId.ToStringInvariant());
            var atrId = AtrIds.GetAtrId(id);
            lock (_initAtrLock)
            {
                // TODO: AtrRepository should be built via factory to actually support mocking.
                _atr ??= new AtrRepository(
                    attemptId: AttemptId,
                    overallContext: _overallContext,
                    atrCollection: atrCollection,
                    atrId: atrId,
                    atrDurability: _config.DurabilityLevel,
                    loggerFactory: _loggerFactory,
                    testHookAtrId: testHookAtrId);
            }
        }

        private void CheckExpiryAndThrow(string? docId, string hookPoint)
        {
            if (HasExpiredClientSide(docId, hookPoint))
            {
                _expirationOvertimeMode = true;
                throw ErrorBuilder.CreateError(this, ErrorClass.FailExpiry)
                    .Cause(new AttemptExpiredException(this, $"Expired in '{hookPoint}'"))
                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                    .Build();
            }
        }

        private void ErrorIfExpiredAndNotInExpiryOvertimeMode(string hookPoint, string? docId = null, [CallerMemberName] string caller = "")
        {
            if (_expirationOvertimeMode)
            {
                Logger.LogInformation("[{attemptId}] not doing expiry check in {hookPoint}/{caller} as already in expiry overtime mode.",
                    AttemptId, hookPoint, caller);
                return;
            }

            if (HasExpiredClientSide(docId, hookPoint))
            {
                Logger.LogInformation("[{attemptId}] has expired in stage {hookPoint}/{caller}", AttemptId, hookPoint, caller);
                throw new AttemptExpiredException(this, $"Attempt has expired in stage {hookPoint}/{caller}");
            }
        }

        internal bool HasExpiredClientSide(string? docId, string place = "")
        {
            try
            {
                HookArgs hookArgs = new(place, docId);
                var over = _overallContext.IsExpired;
                var hook = _testHooks.Sync(HookPoint.HasExpired, this, hookArgs) is true;
                if (over)
                {
                    Logger.LogInformation("expired in stage {place} / attemptId = {attemptId}", place, AttemptId);
                }

                if (hook)
                {
                    Logger.LogInformation("fake expiry in stage {place} / attemptId = {attemptId}", place, AttemptId);
                }

                return over || hook;
            }
            catch
            {
                Logger.LogDebug("fake expiry due to throw in stage {place}", place);
                throw;
            }
        }

        internal async Task CheckWriteWriteConflict(TransactionGetResult gr, string interactionPoint, IRequestSpan? parentSpan)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#CheckWriteWriteConflict
            // This logic checks and handles a document X previously read inside a transaction, A, being involved in another transaction B.
            // It takes a TransactionGetResult gr variable.

            using var traceSpan = TraceSpan(parent: parentSpan);

            // If the transaction has expired, enter ExpiryOvertimeMode and raise Error(ec=FAIL_EXPIRY, raise=TRANSACTION_EXPIRED).
            CheckExpiryAndThrow(gr.Id, DefaultTestHooks.HOOK_CHECK_WRITE_WRITE_CONFLICT);

            var sw = Stopwatch.StartNew();
            await RepeatUntilSuccessOrThrow(async () =>
            {
                var method = nameof(CheckWriteWriteConflict);
                var redactedId = Redactor.UserData(gr.FullyQualifiedId);
                Logger.LogDebug("{method}@{interactionPoint} for {redactedId}, attempt={attemptId}", method, interactionPoint, redactedId, AttemptId);
                await ForwardCompatibility.Check(this, interactionPoint, gr.TransactionXattrs?.ForwardCompatibility).CAF();
                var otherAtrFromDocMeta = gr.TransactionXattrs?.AtrRef;
                if (otherAtrFromDocMeta == null)
                {
                    Logger.LogDebug("{method} no other txn for {redactedId}, attempt={attemptId}", method, redactedId, AttemptId);

                    // If gr has no transaction Metadata, it’s fine to proceed.
                    return RepeatAction.NoRepeat;
                }

                if (gr.TransactionXattrs?.Id?.Transactionid == _overallContext.TransactionId)
                {
                    Logger.LogDebug("{method} same txn for {redactedId}, attempt={attemptId}", method, redactedId, AttemptId);

                    // Else, if transaction A == transaction B, it’s fine to proceed
                    return RepeatAction.NoRepeat;
                }

                // Do a lookupIn call to fetch the ATR entry for B.
                ICouchbaseCollection ? otherAtrCollection = null;
                try
                {
                    _testHooks.Sync(HookPoint.BeforeCheckAtrEntryForBlockingDoc, this, _atr?.AtrId ?? string.Empty);

                    otherAtrCollection = _atr == null
                        ? await AtrRepository.GetAtrCollection(otherAtrFromDocMeta, gr.Collection).CAF()
                        : await _atr.GetAtrCollection(otherAtrFromDocMeta).CAF();
                }
                catch (Exception err)
                {
                    throw ErrorBuilder.CreateError(this, ErrorClass.FailWriteWriteConflict, err)
                        .RetryTransaction()
                        .Build();
                }

                if (otherAtrCollection == null)
                {
                    // we couldn't get the ATR collection, which means that the entry was bad
                    // --OR-- the bucket/collection/scope was deleted/locked/rebalanced
                    throw ErrorBuilder.CreateError(this, ErrorClass.FailHard)
                        .Cause(new Exception(
                            $"ATR entry '{Redactor.UserData(gr?.TransactionXattrs?.AtrRef?.ToString())}' could not be read.",
                            new DocumentNotFoundException()))
                        .Build();
                }

                var txn = gr.TransactionXattrs ?? throw new ArgumentNullException(nameof(gr.TransactionXattrs));
                txn.ValidateMinimum();
                AtrEntry? otherAtr = await AtrRepository.FindEntryForTransaction(otherAtrCollection, txn.AtrRef!.Id!, txn.Id!.AttemptId!).CAF();

                if (otherAtr == null)
                {
                    // cleanup occurred, OK to proceed.
                    Logger.LogDebug("{method} cleanup occurred on other ATR for {redactedId}, attempt={attemptId}", method, redactedId, AttemptId);
                    return RepeatAction.NoRepeat;
                }

                Logger.LogDebug("[{attemptId}] OtherATR.TransactionId = {otherAtrId} in state {otherAtrState}", AttemptId, otherAtr.TransactionId, otherAtr.State);

                await ForwardCompatibility.Check(this, ForwardCompatibility.WriteWriteConflictReadingAtr, otherAtr.ForwardCompatibility).CAF();

                if (otherAtr.State == AttemptStates.COMPLETED || otherAtr.State == AttemptStates.ROLLED_BACK)
                {
                    Logger.LogInformation("[{attemptId}] ATR entry state of {attemptState} indicates we can proceed to overwrite", AttemptId, otherAtr.State);

                    // ok to proceed
                    Logger.LogDebug("{method} other ATR is {otherAtrState} for {redactedId}, attempt={attemptId}",
                        method, otherAtr.State, redactedId, AttemptId);
                    return RepeatAction.NoRepeat;
                }

                if (sw.Elapsed > WriteWriteConflictTimeLimit)
                {
                    Logger.LogWarning("{method} CONFLICT DETECTED. Other ATR TransactionId={otherAtrTransactionid} is {otherAtrState} for document {redactedId}, thisAttempt={transactionId}/{attemptId}",
                        method,
                        otherAtr.TransactionId,
                        otherAtr.State,
                        redactedId,
                        _overallContext.TransactionId,
                        AttemptId);
                    throw ErrorBuilder.CreateError(this, ErrorClass.FailWriteWriteConflict)
                        .RetryTransaction()
                        .Build();
                }
                else
                {
                    Logger.LogDebug("{elapsed}ms elapsed in {method}", sw.Elapsed.TotalMilliseconds, nameof(CheckWriteWriteConflict));
                }

                return RepeatAction.RepeatWithBackoff;
            }).CAF();
        }

        private QueryOptions NonStreamingQuery() => new QueryOptions() { Serializer = _nonStreamingTypeSerializer };

        private async Task<IQueryResult<T>> QueryWrapper<T>(
                long statementId,
                IScope? scope,
                string statement,
                QueryOptions options,
                string hookPoint,
                IRequestSpan? parentSpan,
                bool isBeginWork = false,
                bool existingErrorCheck = true,
                JObject? txdata = null,
                bool txImplicit = false
            )
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            traceSpan.Item?.SetAttribute("db.statement", statement)
                          ?.SetAttribute("db.couchbase.transactions.tximplicit", txImplicit);

            Logger.LogDebug("[{attemptId}] Executing Query: {hookPoint}: {txdata}", AttemptId, hookPoint, Redactor.UserData(txdata?.ToString()));

            if (!_queryMode && !isBeginWork)
            {
                if (!txImplicit)
                {
                    await QueryBeginWork(traceSpan?.Item, scope).CAF();
                }

                _queryMode = true;
            }

            QueryPreCheck(statement, hookPoint, existingErrorCheck);

            if (!isBeginWork && !txImplicit)
            {
                options = options.Raw("txid", AttemptId);
                if (_lastDispatchedQueryNode != null)
                {
                    options.LastDispatchedNode = _lastDispatchedQueryNode;
                }
            }

            if (txdata != null)
            {
                options = options.Raw("txdata", txdata);
            }

            if (txImplicit)
            {
                options = options.Raw("tximplicit", true);
                QueryTxData txdataSingleQuery = CreateBeginWorkTxData();
                options = InitializeBeginWorkQueryOptions(options);
                options = options.Raw("txdata", txdataSingleQuery);
            }

            options = options.Metrics(true);
            try
            {
                try
                {
                    _testHooks.Sync(HookPoint.BeforeQuery, this, statement);
                    IQueryResult<T> results = scope != null
                        ? await scope.QueryAsync<T>(statement, options).CAF()
                        : await _cluster.QueryAsync<T>(statement, options).CAF();

                    // "on success"?  Do we need to check the status, here?
                    _queryContextScope ??= scope;
                    _testHooks.Sync(HookPoint.AfterQuery, this, statement);

                    if (results.MetaData?.Status == QueryStatus.Fatal)
                    {
                        var err = ErrorBuilder.CreateError(this, ErrorClass.FailOther).Build();
                        SaveErrorWrapper(err);
                        throw err;
                    }

                    if (results.MetaData?.LastDispatchedToNode != null)
                    {
                        _lastDispatchedQueryNode = results.MetaData.LastDispatchedToNode;
                    }

                    return results;
                }
                catch (Exception exByQuery)
                {
                    Logger.LogError("[{attemptId}] query failed at {hookPoint}: {cause}", AttemptId, hookPoint, exByQuery);
                    var converted = ConvertQueryError(exByQuery);
                    if (converted is TransactionOperationFailedException err)
                    {
                        SaveErrorWrapper(err);
                    }

                    if (converted == null)
                    {
                        throw;
                    }

                    throw converted;
                }
            }
            catch (TransactionExpiredException err)
            {
                // ExtSingleQuery: As the very final stage .. If err is a TransactionExpiredException, raise an UnambiguousTimeoutException instead.
                throw new UnambiguousTimeoutException("Single Query Transaction timed out", err);
            }
        }

        private QueryTxData CreateBeginWorkTxData()
        {
            var state = new TxDataState((long)_overallContext.RemainingUntilExpiration.TotalMilliseconds);
            // TODO: get the cluster KvTimeout value here.
            long? kvTimeoutMillis = 10_000;
            var txConfig = new TxDataReportedConfig(kvTimeoutMillis ?? 10_000, AtrIds.NumAtrs, _effectiveDurabilityLevel.ToString().ToUpperInvariant());

            var mutations = _stagedMutations?.ToList().Select(sm => sm.AsTxData()) ?? Array.Empty<TxDataMutation>();
            var txid = new CompositeId()
            {
                Transactionid = _overallContext.TransactionId,
                AttemptId = AttemptId
            };

            var atrRef = _atr?.AtrRef;
            if (atrRef == null && _config?.MetadataCollection != null)
            {
                var ks = _config.MetadataCollection!;
                atrRef = new AtrRef()
                {
                    BucketName = ks.Bucket,
                    ScopeName = ks.Scope,
                    CollectionName = ks.Collection
                };
            }

            var txdataSingleQuery = new QueryTxData(txid, state, txConfig, atrRef, mutations);
            return txdataSingleQuery;
        }

        private void QueryPreCheck(string statement, string hookPoint, bool existingErrorCheck)
        {
            DoneCheck();
            if (existingErrorCheck)
            {
                CheckErrors();
            }

            var expiresSoon = _overallContext.RemainingUntilExpiration < ExpiryThreshold;
            var docIdForHook = string.IsNullOrEmpty(statement) ? expiresSoon.ToString() : statement;
            if (HasExpiredClientSide(docId: docIdForHook, place: hookPoint))
            {
                Logger.LogInformation("transaction has expired in stage '{stage}' remaining={remaining} threshold={threshold}",
                    hookPoint, _overallContext.RemainingUntilExpiration.TotalMilliseconds, ExpiryThreshold.TotalMilliseconds);

                throw ErrorBuilder.CreateError(this, ErrorClass.FailExpiry)
                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                    .DoNotRollbackAttempt()
                    .Build();
            }
        }

        // TODO:  This should be internal.  As that's a breaking change, we'll do it with the other minor breaking changes in ExtSdkIntegration
        /// <summary>
        /// INTERNAL
        /// </summary>
        /// <param name="err">INTERNAL</param>
        /// <returns>INTERNAL</returns>
        [InterfaceStability(Level.Volatile)]
        public Exception? ConvertQueryError(Exception err)
        {
            if (err is Couchbase.Core.Exceptions.TimeoutException)
            {
                return ErrorBuilder.CreateError(this, ErrorClass.FailExpiry)
                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                    .Build();
            }
            else if (err is CouchbaseException ce)
            {
                if (ce.Context is QueryErrorContext qec)
                {
                    if (qec.Errors?.Count >= 1)
                    {
                        var chosenError = ChooseQueryError(qec);
                        if (chosenError == null)
                        {
                            return null;
                        }

                        var code = chosenError.Code;
                        switch (code)
                        {
                            case 1065: // Unknown parameter
                                return ErrorBuilder.CreateError(this, ErrorClass.FailOther)
                                    .Cause(new FeatureNotAvailableException("Unknown query parameter: note that query support in transactions is available from Couchbase Server 7.0 onwards"))
                                    .Build();
                            case 1197: // Missing tenant
                                return ErrorBuilder.CreateError(this, ErrorClass.FailOther)
                                    .Cause(new FeatureNotAvailableException("This server requires that a Scope be passed to ctx.query()."))
                                    .Build();
                            case 17004: // Transaction context error
                                return new AttemptNotFoundOnQueryException();
                            case 1080: // Timeout
                            case 17010: // TransactionTimeout
                                return ErrorBuilder.CreateError(this, ErrorClass.FailExpiry)
                                    .Cause(new AttemptExpiredException(this, "expired during query", err))
                                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                                    .Build();
                            case 17012: // Duplicate key
                                return new DocumentExistsException(qec);
                            case 17014: // Key not found
                                return new DocumentNotFoundException(qec);
                            case 17015: // CAS mismatch
                                return new CasMismatchException(qec);
                        }

                        if (chosenError.AdditionalData != null && chosenError.AdditionalData.TryGetValue("cause", out var causeObj))
                        {
                            var errorCause = causeObj switch
                            {
                                JToken jToken => jToken.ToObject<QueryErrorCause>(),
                                JsonElement jsonElement => jsonElement.Deserialize(DataModelSerializerContext.Default.QueryErrorCause),
                                _ => new QueryErrorCause(null, null, null, null)
                            };

                            Logger.LogWarning("query code={code} cause={cause} raise={raise}",
                                code,
                                Redactor.UserData(errorCause?.cause ?? string.Empty),
                                errorCause?.raise ?? string.Empty
                            );

                            var builder = ErrorBuilder.CreateError(this, ErrorClass.FailOther, err);
                            TransactionOperationFailedException.FinalError toRaise = errorCause?.raise switch
                            {
                                "failed_post_commit" => TransactionOperationFailedException.FinalError.TransactionFailedPostCommit,
                                "commit_ambiguous" => TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous,
                                "expired" => TransactionOperationFailedException.FinalError.TransactionExpired,
                                "failed" => TransactionOperationFailedException.FinalError.TransactionFailed,
                                _ => TransactionOperationFailedException.FinalError.TransactionFailed
                            };

                            builder = builder.RaiseException(toRaise);

                            if (errorCause?.retry == true)
                            {
                                builder = builder.RetryTransaction();
                            }

                            if (errorCause?.rollback == false)
                            {
                                builder.DoNotRollbackAttempt();
                            }

                            return builder.Build();
                        }
                    }
                }
            }

            return null;
        }

        private Query.Error? ChooseQueryError(QueryErrorContext qec)
        {
            // Look for a TransactionOperationFailed error from gocbcore
            if (qec.Errors == null)
            {
                return null;
            }

            foreach (var err in qec.Errors)
            {
                if (err.Message.Contains("cause"))
                {
                    return err;
                }
            }

            foreach (var err in qec.Errors)
            {
                if (err.Code >= 17_000 && err.Code < 18_000)
                {
                    return err;
                }
            }

            return qec.Errors.FirstOrDefault();
        }

        private async Task QueryBeginWork(IRequestSpan? parentSpan, IScope? scope)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            Logger.LogInformation("[{attemptId}] Entering query mode", AttemptId);

            // TODO: create and populate txdata fully from existing KV ops
            // TODO: state.timeLeftms
            // TODO: config
            // TODO: handle customMetadataCollection and uninitialized ATR (AtrRef with no Id)
            var txid = new CompositeId()
            {
                Transactionid = _overallContext.TransactionId,
                AttemptId = AttemptId
            };

            var txdata = CreateBeginWorkTxData();
            QueryOptions queryOptions = InitializeBeginWorkQueryOptions(NonStreamingQuery());

            var results = await QueryWrapper<QueryBeginWorkResponse>(
                statementId: 0,
                scope: scope,
                statement: "BEGIN WORK",
                options: queryOptions,
                hookPoint: DefaultTestHooks.HOOK_QUERY_BEGIN_WORK,
                isBeginWork: true,
                existingErrorCheck: true,
                txdata: txdata.ToJson(),
                parentSpan: traceSpan.Item
                ).CAF();

            // NOTE: the txid returned is the AttemptId, not the TransactionId.
            await foreach (var result in results.ConfigureAwait(false))
            {
                if (result.txid != AttemptId)
                {
                    Logger.LogWarning("BEGIN WORK returned '{txid}', expected '{AttemptId}'", result.txid, AttemptId);
                }
                else
                {
                    Logger.LogDebug(result.ToString());
                }
            }
        }

        private QueryOptions InitializeBeginWorkQueryOptions(QueryOptions queryOptions)
        {
            queryOptions
                .ScanConsistency(_config.ScanConsistency ?? QueryScanConsistency.RequestPlus)
                .Raw("durability_level", _effectiveDurabilityLevel switch
                {
                    DurabilityLevel.None => "none",
                    DurabilityLevel.Majority => "majority",
                    DurabilityLevel.MajorityAndPersistToActive => "majorityAndPersistActive",
                    DurabilityLevel.PersistToMajority => "persistToMajority",
                    _ => _effectiveDurabilityLevel.ToString()
                })
                .Raw("txtimeout", $"{_overallContext.RemainingUntilExpiration.TotalMilliseconds}ms");

            if (_config.MetadataCollection != null)
            {
                var mc = _config.MetadataCollection;
                queryOptions.Raw("atrcollection", $"`{mc.Bucket}`.`{mc.Scope}`.`{mc.Collection}`");
            }

            return queryOptions;
        }

        internal void SaveErrorWrapper(TransactionOperationFailedException ex)
        {
            if (!_previousErrors.TryAdd(ex.ExceptionNumber, ex))
            {
                Logger.LogError("Could not add err {ex}", ex);
            }
        }

        private enum RepeatAction
        {
            NoRepeat = 0,
            RepeatWithDelay = 1,
            RepeatNoDelay = 2,
            RepeatWithBackoff = 3
        }

        private async Task<ICouchbaseCollection> GetAtrCollection(AtrRef atrRef, ICouchbaseCollection anyCollection)
        {
            var getCollectionTask = _atr?.GetAtrCollection(atrRef)
                                    ?? AtrRepository.GetAtrCollection(atrRef, anyCollection);
            var docAtrCollection = await getCollectionTask.CAF()
                                   ?? throw new ActiveTransactionRecordNotFoundException();

            return docAtrCollection;
        }

        private async Task<T> RepeatUntilSuccessOrThrow<T>(Func<Task<(RepeatAction retry, T finalVal)>> func, int retryLimit = 100_000, [CallerMemberName] string caller = nameof(RepeatUntilSuccessOrThrow))
        {
            int retryCount = -1;
            int opRetryBackoffMs = 1;
            while (retryCount < retryLimit)
            {
                retryCount++;
                var result = await func().CAF();
                switch (result.retry)
                {
                    case RepeatAction.RepeatWithDelay:
                        await OpRetryDelay().CAF();
                        break;
                    case RepeatAction.RepeatWithBackoff:
                        await Task.Delay(opRetryBackoffMs).CAF();
                        opRetryBackoffMs = Math.Min(opRetryBackoffMs * 10, 100);
                        break;
                    case RepeatAction.RepeatNoDelay:
                        break;
                    default:
                        return result.finalVal;
                }
            }

            throw new InvalidOperationException($"Retry Limit ({retryLimit}) exceeded in method {caller}");
        }

        private Task RepeatUntilSuccessOrThrow(Func<Task<RepeatAction>> func, int retryLimit = 100_000, [CallerMemberName] string caller = nameof(RepeatUntilSuccessOrThrow)) =>
            RepeatUntilSuccessOrThrow<object>(async () =>
            {
                var retry = await func().CAF();
                return (retry, string.Empty);
            }, retryLimit, caller);

        private Task OpRetryDelay() => Task.Delay(Transactions.OpRetryDelay);

        internal CleanupRequest? GetCleanupRequest()
        {
            if (_atr == null
                || _state == AttemptStates.NOTHING_WRITTEN
                || _state == AttemptStates.COMPLETED
                || _state == AttemptStates.ROLLED_BACK)
            {
                // nothing to clean up
                Logger.LogInformation("Skipping addition of cleanup request in state {s}", _state);
                return null;
            }

            var durabilityShort = new ShortStringDurabilityLevel(_effectiveDurabilityLevel).ToString();
            var cleanupRequest = new CleanupRequest(
                AttemptId: AttemptId,
                AtrId: _atr.AtrId,
                AtrCollection: _atr.Collection,
                InsertedIds: _stagedMutations.Inserts().Select(sm => sm.AsDocRecord()).ToList(),
                ReplacedIds: _stagedMutations.Replaces().Select(sm => sm.AsDocRecord()).ToList(),
                RemovedIds: _stagedMutations.Removes().Select(sm => sm.AsDocRecord()).ToList(),
                State: _state,
                WhenReadyToBeProcessed: DateTimeOffset.UtcNow, // EXT_REMOVE_COMPLETED
                ProcessingErrors: new ConcurrentQueue<Exception>(),
                DurabilityLevel: durabilityShort
            )
            ;

            Logger.LogInformation("Adding collection for {col}/{atr} to run at {when}", Redactor.UserData(cleanupRequest.AtrCollection.Name), cleanupRequest.AtrId, cleanupRequest.WhenReadyToBeProcessed);
            return cleanupRequest;
        }

        private DelegatingDisposable<IRequestSpan> TraceSpan([CallerMemberName] string method = "RootSpan", IRequestSpan? parent = null)
            => new DelegatingDisposable<IRequestSpan>(_requestTracer.RequestSpan(method, parent), Logger.BeginMethodScope(method));

        private Exception Error(ErrorClass ec, Exception err, bool? retry = null, bool? rollback = null, TransactionOperationFailedException.FinalError? raise = null, bool setStateBits = true)
        {
            var eb = ErrorBuilder.CreateError(this, ec, err);
            if (retry.HasValue && retry.Value)
            {
                eb.RetryTransaction();
            }

            if (rollback.HasValue && rollback.Value == false)
            {
                eb.DoNotRollbackAttempt();
            }

            if (raise.HasValue)
            {
                eb.RaiseException(raise.Value);
            }

            return eb.Build();
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





