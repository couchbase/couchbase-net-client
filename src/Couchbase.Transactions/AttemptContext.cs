using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Transactions.ActiveTransactionRecords;
using Couchbase.Transactions.Cleanup;
using Couchbase.Transactions.Components;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.DataModel;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.Attempts;
using Couchbase.Transactions.Error.External;
using Couchbase.Transactions.Error.Internal;
using Couchbase.Transactions.Forwards;
using Couchbase.Transactions.Internal;
using Couchbase.Transactions.Internal.Test;
using Couchbase.Transactions.LogUtil;
using Couchbase.Transactions.Support;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using static Couchbase.Transactions.Error.ErrorBuilder;
using Exception = System.Exception;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Couchbase.Transactions
{
    /// <summary>
    /// Provides methods that allow an application's transaction logic to read, mutate, insert, and delete documents.
    /// </summary>
    public class AttemptContext
    {
        private static readonly TimeSpan ExpiryThreshold = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan WriteWriteConflictTimeLimit = TimeSpan.FromSeconds(1);
        private readonly TransactionContext _overallContext;
        private readonly MergedTransactionConfig _config;
        private readonly ITestHooks _testHooks;
        internal IRedactor Redactor { get; }
        private AttemptStates _state = AttemptStates.NOTHING_WRITTEN;
        private readonly ErrorTriage _triage;

        private readonly StagedMutationCollection _stagedMutations = new StagedMutationCollection();
        private readonly object _initAtrLock = new();
        private IAtrRepository? _atr = null;
        private readonly IDocumentRepository _docs;
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
            ITestHooks? testHooks,
            IRedactor redactor,
            ILoggerFactory loggerFactory,
            ICluster cluster,
            IDocumentRepository? documentRepository = null,
            IAtrRepository? atrRepository = null,
            IRequestTracer? requestTracer = null,
            bool singleQueryTransactionMode = false)
        {
            _cluster = cluster;
            _nonStreamingTypeSerializer = NonStreamingSerializerWrapper.FromCluster(_cluster);
            _requestTracer = requestTracer ?? new NoopRequestTracer();
            AttemptId = attemptId ?? throw new ArgumentNullException(nameof(attemptId));
            _overallContext = overallContext ?? throw new ArgumentNullException(nameof(overallContext));
            _config = _overallContext.Config;
            _testHooks = testHooks ?? DefaultTestHooks.Instance;
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _effectiveDurabilityLevel = _config.DurabilityLevel;
            _loggerFactory = loggerFactory;
            Logger = loggerFactory.CreateLogger<AttemptContext>();
            _triage = new ErrorTriage(this, loggerFactory);
            _docs = documentRepository ?? new DocumentRepository(_overallContext, _config.KeyValueTimeout, _effectiveDurabilityLevel, AttemptId, _nonStreamingTypeSerializer);
            _singleQueryTransactionMode = singleQueryTransactionMode;
            if (atrRepository != null)
            {
                _atr = atrRepository;
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
            CheckExpiryAndThrow(id, hookPoint: ITestHooks.HOOK_GET);

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
                    await _testHooks.BeforeDocGet(this, id).CAF();

                    var result = await GetWithMav(collection, id, parentSpan: traceSpan.Item).CAF();

                    await _testHooks.AfterGetComplete(this, id).CAF();
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
                    hookPoint: ITestHooks.HOOK_QUERY_KV_GET,
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

                var classified = CreateError(this, err.Classify(), err).Build();
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

                var findEntryTask = _atr?.FindEntryForTransaction(docAtrCollection, blockingTxn.AtrRef.Id!, blockingTxn.Id!.AttemptId)
                                    ?? AtrRepository.FindEntryForTransaction(docAtrCollection, blockingTxn.AtrRef.Id!,
                                        blockingTxn.Id!.AttemptId, _config.KeyValueTimeout);

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
                throw CreateError(this, ErrorClass.FailDocNotFound, new DocumentNotFoundException()).Build();
            }

            CheckExpiryAndThrow(doc.Id, ITestHooks.HOOK_REPLACE);
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
                    hookPoint: ITestHooks.HOOK_QUERY_KV_REPLACE,
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
                var builder = CreateError(this, err.Classify(), err);
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
                    await _testHooks.BeforeStagedReplace(this, doc.Id).CAF();
                    var contentWrapper = new JObjectContentWrapper(content);
                    bool isTombstone = doc.Cas == 0;
                    (var updatedCas, var mutationToken) = await _docs.MutateStagedReplace(doc, content, _atr, accessDeleted).CAF();
                    Logger.LogDebug("{method} for {redactedId}, attemptId={attemptId}, preCase={preCas}, postCas={postCas}, accessDeleted={accessDeleted}", nameof(CreateStagedReplace), Redactor.UserData(doc.Id), AttemptId, doc.Cas, updatedCas, accessDeleted);
                    await _testHooks.AfterStagedReplaceComplete(this, doc.Id).CAF();

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

            CheckExpiryAndThrow(id, hookPoint: ITestHooks.HOOK_INSERT);

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
                    hookPoint: ITestHooks.HOOK_QUERY_KV_INSERT,
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

                var builder = CreateError(this, err.Classify(), err);
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
                        ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_CREATE_STAGED_INSERT, id);

                        await _testHooks.BeforeStagedInsert(this, id).CAF();
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

                        await _testHooks.AfterStagedInsertComplete(this, id).CAF();

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
                                        await _testHooks.BeforeGetDocInExistsDuringStagedInsert(this, id).CAF();
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

                                        // Else if the doc is not in a transaction
                                        // -> Raise Error(FAIL_DOC_ALREADY_EXISTS, cause=DocumentExistsException).
                                        // There is logic further up the stack that handles this by fast-failing the transaction.
                                        if (!docInATransaction)
                                        {
                                            throw CreateError(this, ErrorClass.FailDocAlreadyExists)
                                                .Cause(new DocumentExistsException())
                                                .Build();
                                        }
                                        else
                                        {
                                            // TODO: BF-CBD-3787
                                            var operationType = docWithMeta?.TransactionXattrs?.Operation?.Type;
                                            if (operationType != "insert")
                                            {
                                                Logger.LogWarning("BF-CBD-3787 FAIL_DOC_ALREADY_EXISTS here because type = {operationType}", operationType);
                                                throw CreateError(this, ErrorClass.FailDocAlreadyExists, new DocumentExistsException()).Build();
                                            }

                                            // Else call the CheckWriteWriteConflict logic, which conveniently does everything we need to handle the above cases.
                                            var getResult = docWithMeta!.GetPostTransactionResult();
                                            await CheckWriteWriteConflict(getResult, ForwardCompatibility.WriteWriteConflictInserting, traceSpan.Item).CAF();

                                            // BF-CBD-3787: If the document is a staged insert but also is not a tombstone (e.g. it is from protocol 1.0), it must be deleted first
                                            if (operationType == "insert" && !isTombstone)
                                            {
                                                try
                                                {
                                                    await _testHooks.BeforeOverwritingStagedInsertRemoval(this, id).CAF();
                                                    await _docs.UnstageRemove(collection, id, getResult.Cas).CAF();
                                                }
                                                catch (Exception err)
                                                {
                                                    var ec = err.Classify();
                                                    switch (ec)
                                                    {
                                                        case ErrorClass.FailDocNotFound:
                                                        case ErrorClass.FailCasMismatch:
                                                            throw CreateError(this, ec, err).RetryTransaction().Build();
                                                        default:
                                                            throw CreateError(this, ec, err).Build();
                                                    }
                                                }

                                                // hack workaround for NCBC-2944
                                                // Supposed to "retry this (CreateStagedInsert) algorithm with the cas from the Remove", but we don't have a Cas from the Remove.
                                                // Instead, we just trigger a retry of the entire transaction, since this is such an edge case.
                                                throw CreateError(this, ErrorClass.FailDocAlreadyExists, ex).RetryTransaction().Build();
                                            }

                                            // If this logic succeeds, we are ok to overwrite the doc.
                                            // Perform this algorithm (createStagedInsert) from the top, with cas=the cas from the get.
                                            cas = docWithMeta.Cas;
                                            return (RepeatAction.NoRepeat, RepeatAction.RepeatNoDelay);
                                        }
                                    }
                                    catch (Exception exDocExists)
                                    {
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
                        ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_ATR_PENDING);
                        await _testHooks.BeforeAtrPending(this).CAF();
                        var t1 = _overallContext.StartTime;
                        var t2 = DateTimeOffset.UtcNow;
                        var tElapsed = t2 - t1;
                        var tc = _config.ExpirationTime;
                        var tRemaining = tc - tElapsed;
                        var exp = (ulong)Math.Max(Math.Min(tRemaining.TotalMilliseconds, tc.TotalMilliseconds), 0);
                        await _atr.MutateAtrPending(exp, docDurability).CAF();
                        Logger?.LogDebug($"{nameof(SetAtrPending)} for {Redactor.UserData(_atr.FullPath)} (attempt={AttemptId})");
                        await _testHooks.AfterAtrPending(this).CAF();
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
            CheckExpiryAndThrow(doc.Id, ITestHooks.HOOK_REMOVE);

            var stagedOld = _stagedMutations.Find(doc);
            if (stagedOld != null)
            {
                if (stagedOld != null)
                {
                    if (stagedOld.Type == StagedMutationType.Remove)
                    {
                        throw CreateError(this, ErrorClass.FailDocNotFound, new DocumentNotFoundException()).Build();
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
                    hookPoint: ITestHooks.HOOK_QUERY_KV_REMOVE,
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
                var builder = CreateError(this, err.Classify(), err);
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
                    await _testHooks.BeforeStagedRemove(this, doc.Id).CAF();
                    (var updatedCas, var mutationToken) = await _docs.MutateStagedRemove(doc, _atr!).CAF();
                    Logger?.LogDebug($"{nameof(CreateStagedRemove)} for {Redactor.UserData(doc.Id)}, attemptId={AttemptId}, preCas={doc.Cas}, postCas={updatedCas}");
                    await _testHooks.AfterStagedRemoveComplete(this, doc.Id).CAF();

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
                throw CreateError(this, ErrorClass.FailExpiry)
                    .Cause(new AttemptExpiredException(this, "Expired in 'removeStagedInsert'"))
                    .DoNotRollbackAttempt()
                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                    .Build();
            }

            try
            {
                await _testHooks.BeforeRemoveStagedInsert(this, doc.Id).CAF();
                (var removedCas, _) = await _docs.RemoveStagedInsert(doc).CAF();
                await _testHooks.AfterRemoveStagedInsert(this, doc.Id).CAF();
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
                    throw CreateError(this, ec, err).DoNotRollbackAttempt().Build();
                }

                throw CreateError(this, ec, err).RetryTransaction().Build();
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
            CheckExpiryAndThrow(null, ITestHooks.HOOK_BEFORE_COMMIT);
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
                    hookPoint: ITestHooks.HOOK_QUERY_COMMIT,
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
                    throw CreateError(this, ec, err)
                        .RaiseException(TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous)
                        .DoNotRollbackAttempt()
                        .Build();
                }

                throw CreateError(this, ec, err).DoNotRollbackAttempt().Build();
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
            if (HasExpiredClientSide(null, ITestHooks.HOOK_ATR_COMPLETE) && !_expirationOvertimeMode)
            {
                // If transaction has expired and not in ExpiryOvertimeMode: though technically expired, the transaction should be regarded
                // as successful, as this is just a cleanup step.
                // Return success.
                return;
            }

            try
            {
                await _testHooks.BeforeAtrComplete(this).CAF();
                await _atr!.MutateAtrComplete().CAF();
                Logger?.LogDebug($"{nameof(SetAtrComplete)} for {Redactor.UserData(_atr.FullPath)} (attempt={AttemptId})");
                await _testHooks.AfterAtrComplete(this).CAF();
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
                        await UnstageInsertOrReplace(sm, cas, content, insertMode: true, ambiguityResolutionMode: false).CAF();
                        break;
                    case StagedMutationType.Replace:
                        await UnstageInsertOrReplace(sm, cas, content, insertMode: false, ambiguityResolutionMode: false).CAF();
                        break;
                    default:
                        throw new InvalidOperationException($"Cannot un-stage transaction mutation of type {sm.Type}");
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
                    await _testHooks.BeforeDocRemoved(this, sm.Doc.Id).CAF();
                    if (!_expirationOvertimeMode && HasExpiredClientSide(sm.Doc.Id, ITestHooks.HOOK_REMOVE_DOC))
                    {
                        _expirationOvertimeMode = true;
                    }

                    await _docs.UnstageRemove(sm.Doc.Collection, sm.Doc.Id).CAF();
                    Logger.LogDebug("Unstaged RemoveAsync successfully for {redactedId)} (retryCount={retryCount}", Redactor.UserData(sm.Doc.FullyQualifiedId), retryCount);
                    await _testHooks.AfterDocRemovedPreRetry(this, sm.Doc.Id).CAF();

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
            await _testHooks.AfterDocRemovedPostRetry(this, sm.Doc.Id).CAF();
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
                    if (!_expirationOvertimeMode && HasExpiredClientSide(sm.Doc.Id, ITestHooks.HOOK_COMMIT_DOC))
                    {
                        _expirationOvertimeMode = true;
                    }

                    await _testHooks.BeforeDocCommitted(this, sm.Doc.Id).CAF();
                    (ulong updatedCas, MutationToken mutationToken) = await _docs.UnstageInsertOrReplace(sm.Doc.Collection, sm.Doc.Id, cas, content, insertMode).CAF();
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

                    await _testHooks.AfterDocCommittedBeforeSavingCas(this, sm.Doc.Id).CAF();

                    sm.Doc.Cas = updatedCas;
                    await _testHooks.AfterDocCommitted(this, sm.Doc.Id).CAF();

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
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_ATR_COMMIT);
                    await _testHooks.BeforeAtrCommit(this).CAF();
                    await _atr.MutateAtrCommit(_stagedMutations.ToList()).CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})", nameof(SetAtrCommit), Redactor.UserData(_atr.FullPath), AttemptId);
                    await _testHooks.AfterAtrCommit(this).CAF();
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

                    throw Error(ec, err);
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
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_ATR_COMMIT_AMBIGUITY_RESOLUTION);
                    await _testHooks.BeforeAtrCommitAmbiguityResolution(this).CAF();
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
                    throw CreateError(this, ErrorClass.FailOther)
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
                        throw CreateError(this, ErrorClass.FailOther).RetryTransaction().Build();
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
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_ATR_ABORT);
                    await _testHooks.BeforeAtrAborted(this).CAF();
                    await _atr!.MutateAtrAborted(_stagedMutations.ToList()).CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})", nameof(SetAtrAborted), Redactor.UserData(_atr.FullPath), AttemptId);
                    await _testHooks.AfterAtrAborted(this).CAF();
                    _state = AttemptStates.ABORTED;
                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    if (_expirationOvertimeMode)
                    {
                        throw CreateError(this, ErrorClass.FailExpiry)
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
                            throw toThrow ?? CreateError(this, ec, new InvalidOperationException("Failed to generate proper exception wrapper", ex))
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
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_ATR_ROLLBACK_COMPLETE);
                    await _testHooks.BeforeAtrRolledBack(this).CAF();
                    await _atr!.MutateAtrRolledBack().CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})",
                        nameof(SetAtrRolledBack),
                        Redactor.UserData(_atr.FullPath),
                        AttemptId);
                    await _testHooks.AfterAtrRolledBack(this).CAF();
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
                            throw toThrow ?? CreateError(this, ec,
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

        /// <summary>
        /// Run a query in transaction mode.
        /// </summary>
        /// <typeparam name="T">The type of the result.  Use <see cref="object"/> for queries with no results.</typeparam>
        /// <param name="statement">The statement to execute.</param>
        /// <param name="config">The configuration to use for this query.</param>
        /// <param name="scope">The scope</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="SingleQueryTransactionResult{T}"/> with the query results, if any.</returns>
        /// <remarks>IMPORTANT: Any KV operations after this query will be run via the query engine, which has performance implications.</remarks>
        public Task<IQueryResult<T>> QueryAsync<T>(string statement, TransactionQueryConfigBuilder? config = null, IScope? scope = null, IRequestSpan? parentSpan = null)
        {
            var options = config?.Build() ?? new TransactionQueryOptions();
            return QueryAsync<T>(statement, options, scope, parentSpan);
        }

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
            var traceSpan = TraceSpan(parent: parentSpan);
            long fixmeStatementId = 0;
            var results = await QueryWrapper<T>(
                statementId: fixmeStatementId,
                scope: scope,
                statement: statement,
                options: options.Build(txImplicit),
                hookPoint: ITestHooks.HOOK_QUERY,
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
                if (HasExpiredClientSide(null, hookPoint: ITestHooks.HOOK_ROLLBACK))
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
                    hookPoint: ITestHooks.HOOK_QUERY_ROLLBACK,
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

                var toSave = CreateError(this, err.Classify(), err)
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
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_DELETE_INSERTED, sm.Doc.Id);
                    await _testHooks.BeforeRollbackDeleteInserted(this, sm.Doc.Id).CAF();
                    await _docs.ClearTransactionMetadata(sm.Doc.Collection, sm.Doc.Id, sm.Doc.Cas, true).CAF();
                    Logger.LogDebug("Rolled back staged {type} for {redactedId}", sm.Type, Redactor.UserData(sm.Doc.Id));
                    await _testHooks.AfterRollbackDeleteInserted(this, sm.Doc.Id).CAF();
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
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(ITestHooks.HOOK_ROLLBACK_DOC, sm.Doc.Id);
                    await _testHooks.BeforeDocRolledBack(this, sm.Doc.Id).CAF();
                    await _docs.ClearTransactionMetadata(sm.Doc.Collection, sm.Doc.Id, sm.Doc.Cas, sm.Doc.IsDeleted).CAF();
                    Logger.LogDebug("Rolled back staged {type} for {redactedId}", sm.Type, Redactor.UserData(sm.Doc.Id));
                    await _testHooks.AfterRollbackReplaceOrRemove(this, sm.Doc.Id).CAF();
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
                throw CreateError(this, ErrorClass.FailOther)
                    .Cause(new InvalidOperationException("Cannot perform operations after a transaction has been committed or rolled back."))
                    .DoNotRollbackAttempt()
                    .Build();
            }
        }

        private void BailoutIfInOvertime(bool rollback, [CallerMemberName] string caller = nameof(BailoutIfInOvertime))
        {
            if (_expirationOvertimeMode)
            {
                var builder = CreateError(this, ErrorClass.FailExpiry)
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
            var atrCollection = _config.MetadataCollection ?? collection.Scope.Bucket.DefaultCollection();
            var testHookAtrId = await _testHooks.AtrIdForVBucket(this, AtrIds.GetVBucketId(id)).CAF();
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
                throw CreateError(this, ErrorClass.FailExpiry)
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

        internal bool HasExpiredClientSide(string? docId, [CallerMemberName] string hookPoint = "")
        {
            try
            {
                var over = _overallContext.IsExpired;
                var hook = _testHooks.HasExpiredClientSideHook(this, hookPoint, docId);
                if (over)
                {
                    Logger.LogInformation("expired in stage {hookPoint} / attemptId = {attemptId}", hookPoint, AttemptId);
                }

                if (hook)
                {
                    Logger.LogInformation("fake expiry in stage {hookPoint} / attemptId = {attemptId}", hookPoint, AttemptId);
                }

                return over || hook;
            }
            catch
            {
                Logger.LogDebug("fake expiry due to throw in stage {hookPoint}", hookPoint);
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
            CheckExpiryAndThrow(gr.Id, ITestHooks.HOOK_CHECK_WRITE_WRITE_CONFLICT);

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
                    await _testHooks.BeforeCheckAtrEntryForBlockingDoc(this, _atr?.AtrId ?? string.Empty).CAF();

                    otherAtrCollection = _atr == null
                        ? await AtrRepository.GetAtrCollection(otherAtrFromDocMeta, gr.Collection).CAF()
                        : await _atr.GetAtrCollection(otherAtrFromDocMeta).CAF();
                }
                catch (Exception err)
                {
                    throw CreateError(this, ErrorClass.FailWriteWriteConflict, err)
                        .RetryTransaction()
                        .Build();
                }

                if (otherAtrCollection == null)
                {
                    // we couldn't get the ATR collection, which means that the entry was bad
                    // --OR-- the bucket/collection/scope was deleted/locked/rebalanced
                    throw CreateError(this, ErrorClass.FailHard)
                        .Cause(new Exception(
                            $"ATR entry '{Redactor.UserData(gr?.TransactionXattrs?.AtrRef?.ToString())}' could not be read.",
                            new DocumentNotFoundException()))
                        .Build();
                }

                var txn = gr.TransactionXattrs ?? throw new ArgumentNullException(nameof(gr.TransactionXattrs));
                txn.ValidateMinimum();
                AtrEntry? otherAtr = _atr == null
                    ? await AtrRepository.FindEntryForTransaction(otherAtrCollection, txn.AtrRef!.Id!, txn.Id!.AttemptId!, _config.KeyValueTimeout).CAF()
                    : await _atr.FindEntryForTransaction(otherAtrCollection, txn.AtrRef!.Id!, txn.Id?.AttemptId).CAF();

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
                    throw CreateError(this, ErrorClass.FailWriteWriteConflict)
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
                    await _testHooks.BeforeQuery(this, statement).CAF();
                    IQueryResult<T> results = scope != null
                        ? await scope.QueryAsync<T>(statement, options).CAF()
                        : await _cluster.QueryAsync<T>(statement, options).CAF();

                    // "on success"?  Do we need to check the status, here?
                    _queryContextScope ??= scope;
                    await _testHooks.AfterQuery(this, statement).CAF();

                    if (results.MetaData?.Status == QueryStatus.Fatal)
                    {
                        var err = CreateError(this, ErrorClass.FailOther).Build();
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
            var txConfig = new TxDataReportedConfig((long?)_config?.KeyValueTimeout?.TotalMilliseconds ?? 10_000, AtrIds.NumAtrs, _effectiveDurabilityLevel.ToString().ToUpperInvariant());

            var mutations = _stagedMutations?.ToList().Select(sm => sm.AsTxData()) ?? Array.Empty<TxDataMutation>();
            var txid = new CompositeId()
            {
                Transactionid = _overallContext.TransactionId,
                AttemptId = AttemptId
            };

            var atrRef = _atr?.AtrRef;
            if (atrRef == null && _config?.MetadataCollection != null)
            {
                var col = _config.MetadataCollection;
                var scp = col.Scope;
                var bkt = scp.Bucket;
                atrRef = new AtrRef()
                {
                    BucketName = bkt.Name,
                    ScopeName = scp.Name,
                    CollectionName = col.Name
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
            if (HasExpiredClientSide(docId: docIdForHook, hookPoint: hookPoint))
            {
                Logger.LogInformation("transaction has expired in stage '{stage}' remaining={remaining} threshold={threshold}",
                    hookPoint, _overallContext.RemainingUntilExpiration.TotalMilliseconds, ExpiryThreshold.TotalMilliseconds);

                throw CreateError(this, ErrorClass.FailExpiry)
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
                return CreateError(this, ErrorClass.FailExpiry)
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
                                return CreateError(this, ErrorClass.FailOther)
                                    .Cause(new FeatureNotAvailableException("Unknown query parameter: note that query support in transactions is available from Couchbase Server 7.0 onwards"))
                                    .Build();
                            case 1197: // Missing tenant
                                return CreateError(this, ErrorClass.FailOther)
                                    .Cause(new FeatureNotAvailableException("This server requires that a Scope be passed to ctx.query()."))
                                    .Build();
                            case 17004: // Transaction context error
                                return new AttemptNotFoundOnQueryException();
                            case 1080: // Timeout
                            case 17010: // TransactionTimeout
                                return CreateError(this, ErrorClass.FailExpiry)
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

                            var builder = CreateError(this, ErrorClass.FailOther, err);
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
                hookPoint: ITestHooks.HOOK_QUERY_BEGIN_WORK,
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
                    DurabilityLevel.MajorityAndPersistToActive => "majorityAndPersistToActive",
                    DurabilityLevel.PersistToMajority => "persistToMajority",
                    _ => _effectiveDurabilityLevel.ToString()
                })
                .Raw("txtimeout", $"{_overallContext.RemainingUntilExpiration.TotalMilliseconds}ms");

            if (_config.MetadataCollection != null)
            {
                var mc = _config.MetadataCollection;
                queryOptions.Raw("atrcollection", $"`{mc.Scope.Bucket.Name}`.`{mc.Scope.Name}`.`{mc.Name}`");
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

        private Exception Error(ErrorClass ec, Exception err, bool? retry = null, bool? rollback = null, TransactionOperationFailedException.FinalError? raise = null)
        {
            var eb = CreateError(this, ec, err);
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
