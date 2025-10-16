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
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Client.Transactions.ActiveTransactionRecords;
using Couchbase.Client.Transactions.Cleanup;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.Attempts;
using Couchbase.Client.Transactions.Error.External;
using Couchbase.Client.Transactions.Error.Internal;
using Couchbase.Client.Transactions.Forwards;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Client.Transactions.Internal.Test;
using Couchbase.Client.Transactions.LogUtil;
using Couchbase.Client.Transactions.Support;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using static Couchbase.Client.Transactions.Error.ErrorBuilder;
using Exception = System.Exception;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Couchbase.Client.Transactions
{
    /// <summary>
    /// Provides methods that allow an application's transaction logic to read, mutate, insert, and delete documents.
    /// </summary>
    public class AttemptContext
    {
        private static readonly TimeSpan ExpiryThreshold = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan WriteWriteConflictTimeLimit = TimeSpan.FromSeconds(1);
        internal readonly TransactionContext _overallContext;
        private readonly MergedTransactionConfig _config;
        private readonly ITestHooks _testHooks;
        private readonly int _unstagingConcurrency = 100;
        internal IRedactor Redactor { get; }
        internal AttemptStates AttemptState = AttemptStates.NOTHING_WRITTEN;
        private readonly ErrorTriage _triage;

        private readonly StagedMutationCollection _stagedMutations = new();
        internal volatile IAtrRepository? _atr;
        internal readonly IDocumentRepository _docs;
        private readonly DurabilityLevel _effectiveDurabilityLevel;
        private readonly List<MutationToken> _finalMutations = [];

        private readonly ConcurrentDictionary<long, TransactionOperationFailedException> _previousErrors =
            new ();

        private bool _expirationOvertimeMode = false;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ICluster _cluster;
        private readonly ITypeSerializer _nonStreamingTypeSerializer;
        private readonly IRequestTracer _requestTracer;
        private Uri? _lastDispatchedQueryNode = null;
        private bool _singleQueryTransactionMode = false;
        private IScope? _queryContextScope = null;
        private OperationWrapper _opWrapper;
        private readonly SemaphoreSlim _attemptLock = new(1, 1); // works like a mutex
        internal StateFlags StateFlags { get; }
        internal StagedMutationCollection StagedMutations => _stagedMutations;

        /// <summary>
        /// Gets the ID of this individual attempt.
        /// </summary>
        public string AttemptId { get; }

        /// <summary>
        /// Gets the ID of this overall transaction.
        /// </summary>
        public string TransactionId => _overallContext.TransactionId;

        internal bool UnstagingComplete { get; private set; }

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
            _opWrapper = new OperationWrapper(this, loggerFactory);
            _triage = new ErrorTriage(this, loggerFactory);
            _docs = documentRepository ?? new DocumentRepository(_overallContext, _config.KeyValueTimeout,
                _effectiveDurabilityLevel, AttemptId, _nonStreamingTypeSerializer);
            _singleQueryTransactionMode = singleQueryTransactionMode;
            if (atrRepository != null)
            {
                _atr = atrRepository;
            }

            StateFlags = new StateFlags();
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
        /// <param name="options">Optional instance of TransactionGetOptionsBuilder</param>
        /// <returns>A <see cref="TransactionGetResult"/> containing the document.</returns>
        /// <exception cref="DocumentNotFoundException">If the document does not exist.</exception>
        public async Task<TransactionGetResult> GetAsync(ICouchbaseCollection collection, string id, TransactionGetOptionsBuilder? options = null)
        {
            var getResult = await GetOptionalAsync(collection, id, options).CAF();
            if (getResult == null)
            {
                throw new DocumentNotFoundException();
            }

            return getResult;
        }

        public Task<TransactionGetResult?> GetOptionalAsync(ICouchbaseCollection collection,
            string id)
        {
            var builder = TransactionGetOptionsBuilder.Default;
            return GetOptionalAsync(collection, id, builder);
        }
        public async Task<TransactionGetResult?> GetOptionalAsync(ICouchbaseCollection collection,
            string id, TransactionGetOptionsBuilder? options = null)
        {
            options ??= TransactionGetOptionsBuilder.Default;
            var opt = options.Build();

            return await _opWrapper.WrapOperationAsync(() => GetWithKv(collection, id, opt),
                () => GetWithQuery(collection, id, opt)).CAF();
        }
        /// <summary>
        /// Gets a document or null.
        /// </summary>
        /// <param name="collection">The collection to look up the document in.</param>
        /// <param name="id">The ID of the document.</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="TransactionGetResult"/> containing the document, or null if  not found.</returns>
        public Task<TransactionGetResult?> GetOptionalAsync(ICouchbaseCollection collection, string id,
            IRequestSpan? parentSpan)
        {
            var options =  TransactionGetOptionsBuilder.Default;
            if (parentSpan != null)
            {
                options.Span(parentSpan);
            }
            return  GetOptionalAsync(collection, id, options);
        }
        /// <summary>
        /// This gets from the preferred group, assuming one was set when configuring the cluster
        /// this transaction is using.
        /// </summary>
        /// <param name="collection">Collection where the document should reside.</param>
        /// <param name="id">The ID of the document you wish to get.</param>
        /// <param name="options">Options to use when getting <see cref="TransactionGetOptionsBuilder"/> </param>
        /// <returns>TransactionGetResult representing the document.</returns>
        /// <exception cref="TransactionOperationFailedException"></exception>
        /// <exception cref="FeatureNotAvailableException"> Raised when the cluster doesn't support this feature. </exception>
        /// <exception cref="DocumentUnretrievableException"> Raised when the document was not found in the replicas. </exception>
        public async Task<TransactionGetResult?> GetReplicaFromPreferredServerGroup(
            ICouchbaseCollection collection, string id,
            TransactionGetOptionsBuilder? options = null)
        {
            var opt = (options ??= TransactionGetOptionsBuilder.Default).Build();

            return await _opWrapper.WrapOperationAsync(
                () => GetWithKv(collection, id, opt, allowReplica: true),
                () => throw CreateError(this, ErrorClass.FailOther,
                        new FeatureNotAvailableException(
                            "GetReplicaFromPreferredServerGroup cannot be mixed with query operations"))
                    .Build()).CAF();

        }

        [InterfaceStability(Level.Volatile)]
        public async Task<TransactionGetMultiResult> GetMulti(List<TransactionGetMultiSpec> specs, TransactionGetMultiOptionsBuilder? options = null)
        {
            return await _opWrapper.WrapOperationAsync(
                () => GetMultiInternal(specs, (options ?? TransactionGetMultiOptionsBuilder.Default).Build()),
                () => throw
                        new FeatureNotAvailableException(
                            "GetMulti cannot be mixed with query operations")).CAF();
        }

        [InterfaceStability(Level.Volatile)]
        public async Task<TransactionGetMultiReplicaFromPreferredServerGroupResult>
            GetMultiReplicaFromPreferredServerGroup(
                List<TransactionGetMultiReplicaFromPreferredServerGroupSpec> specs,
                TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder? options = null)
        {
            return await _opWrapper.WrapOperationAsync(
                () => GetMultiInternal(specs, (options ?? TransactionGetMultiReplicaFromPreferredServerGroupOptionsBuilder.Default).Build()),
                () => throw
                    new FeatureNotAvailableException(
                        "GetMulti cannot be mixed with query operations")).CAF();
        }

        private async Task<TransactionGetMultiResult> GetMultiInternal(
            List<TransactionGetMultiSpec> specs, TransactionGetMultiOptions options)
        {
            var mgr = new GetMultiManager<TransactionGetMultiSpec, TransactionGetMultiResult>(this,
                _loggerFactory, _config.KeyValueTimeout, specs, options);
            return await mgr.RunAsync().CAF();
        }

        private async Task<TransactionGetMultiReplicaFromPreferredServerGroupResult>
            GetMultiInternal(
                List<TransactionGetMultiReplicaFromPreferredServerGroupSpec> specs,
                TransactionGetMultiReplicaFromPreferredServerGroupOptions options)
        {
            var mgr =
                new GetMultiManager<TransactionGetMultiReplicaFromPreferredServerGroupSpec,
                    TransactionGetMultiReplicaFromPreferredServerGroupResult>(this, _loggerFactory,
                    _config.KeyValueTimeout, specs, options);
            return await mgr.RunAsync().CAF();
        }

        private async Task<TransactionGetResult?> GetWithKv(ICouchbaseCollection collection, string id,
            TransactionGetOptions options, bool allowReplica = false)
        {
            using var traceSpan = TraceSpan(parent: options.Span);
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
                        // we will go ahead and read the doc from the server below in this case
                        break;
                    case StagedMutationType.Remove:
                        // if we staged a Remove, then we will not read the doc from the server
                        // as even if it changed, we still want this txn to remove it.
                        return null;
                    default:
                        throw new InvalidOperationException(
                            $"Document '{Redactor.UserData(id)}' was staged with type {staged.Type}");
                }
            }
            try
            {
                try
                {
                    await _testHooks.BeforeDocGet(this, id).CAF();

                    var result = await GetWithMav(collection, id, options.Transcoder,
                        parentSpan: traceSpan.Item, allowReplica: allowReplica).CAF();

                    await _testHooks.AfterGetComplete(this, id).CAF();
                    await ForwardCompatibility.Check(this, ForwardCompatibility.Gets,
                        result?.TransactionXattrs?.ForwardCompatibility).CAF();
                    return result;
                }
                catch (Exception ex) when (ex is FeatureNotAvailableException ||
                                           ex is DocumentUnretrievableException)
                {
                    throw;
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

        private async Task<TransactionGetResult?> GetWithQuery(ICouchbaseCollection collection, string id,
            TransactionGetOptions options)
        {
            using var traceSpan = TraceSpan(parent: options.Span);
            try
            {
                var queryOptions = NonStreamingQuery().Parameter(collection.MakeKeyspace())
                    .Parameter(id);
                using var queryResult = await QueryWrapper<QueryGetResult>(0, _queryContextScope, "EXECUTE __get",
                    options: queryOptions,
                    hookPoint: DefaultTestHooks.HOOK_QUERY_KV_GET,
                    txdata: new { kv = true },
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

        private async Task<TransactionGetResult?> GetWithMav(ICouchbaseCollection collection, string id,
            ITypeTranscoder? transcoder, string? resolveMissingAtrEntry = null, IRequestSpan? parentSpan = null,
            bool allowReplica = false)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // we need to resolve the state of that transaction. Here is where we do the “Monotonic Atomic View” (MAV) logic
            try
            {
                // Do a Sub-Document lookup, getting all transactional metadata, the “$document” virtual xattr,
                // and the document’s body. Timeout is set as in Timeouts.
                var docLookupResult = await _docs.LookupDocumentAsync(collection, id, fullDocument: true, transcoder: transcoder, allowReplica: allowReplica).CAF();
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
                    // This is our second attempt getting the document, and it’s in the same state as before, meaning
                    // the transaction that has staged things here is lost, and never committed.
                    return docLookupResult!.IsDeleted
                        ? TransactionGetResult.Empty
                        : docLookupResult.GetPreTransactionResult();
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

                var findEntryTask =
                    _atr?.FindEntryForTransaction(docAtrCollection, blockingTxn.AtrRef.Id!, blockingTxn.Id!.AttemptId)
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
                    return await GetWithMav(collection, id, transcoder,
                        resolveMissingAtrEntry = blockingTxn.Id!.AttemptId,
                        traceSpan.Item).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is FeatureNotAvailableException ||
                                           ex is DocumentUnretrievableException)
                {
                    // seems you did a zone-aware get and it failed, we don't make TransactionOperationFailed
                    // exceptions for these, they go back to the lambda directly.
                    Logger.LogInformation("Got {ex} during GetWithMav", ex);
                    throw;
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

                return await GetWithMav(collection, id, transcoder, resolveMissingAtrEntry).CAF();
            }
            catch (ActiveTransactionRecordEntryNotFoundException ex)
            {
                Logger.LogWarning("ATR entry not found: {ex}", ex);
                if (resolveMissingAtrEntry == null)
                {
                    throw;
                }

                return await GetWithMav(collection, id, transcoder, resolveMissingAtrEntry).CAF();
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

        public Task<TransactionGetResult> ReplaceAsync(TransactionGetResult doc, object content,
            TransactionReplaceOptionsBuilder? options = null)
        {
            options ??= TransactionReplaceOptionsBuilder.Default;
            var opts = options.Build();
            return _opWrapper.WrapOperationAsync(() => ReplaceWithKv(doc, content, opts),
                () => ReplaceWithQuery(doc, content, opts));

        }

        /// <summary>
        /// Replace the content of a document previously fetched in this transaction with new content.
        /// </summary>
        /// <param name="doc">The <see cref="TransactionGetResult"/> of a document previously looked up in this transaction.</param>
        /// <param name="content">The updated content.</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="TransactionGetResult"/> reflecting the updated content.</returns>
        public Task<TransactionGetResult> ReplaceAsync(TransactionGetResult doc, object content,
            IRequestSpan? parentSpan)
        {
            var options =  TransactionReplaceOptionsBuilder.Default;
            if (parentSpan != null) options.Span(parentSpan);
            return ReplaceAsync(doc, content, options);
        }

        private async Task<TransactionGetResult> ReplaceWithKv(TransactionGetResult doc, object content,
            TransactionReplaceOptions options)
        {
            using var traceSpan = TraceSpan(parent: options.Span);
            DoneCheck();
            CheckErrors();

            var stagedOld = _stagedMutations.Find(doc);
            if (stagedOld?.Type == StagedMutationType.Remove)
            {
                throw CreateError(this, ErrorClass.FailDocNotFound, new DocumentNotFoundException()).Build();
            }

            CheckExpiryAndThrow(doc.Id, DefaultTestHooks.HOOK_REPLACE);
            await CheckWriteWriteConflict(doc, ForwardCompatibility.WriteWriteConflictReplacing, traceSpan.Item).CAF();
            await InitAtrAndSetPendingIfNeeded(doc.Collection, doc.Id, traceSpan.Item).CAF();

            var opId = Guid.NewGuid().ToString();
            if (stagedOld?.Type == StagedMutationType.Insert)
            {
                return await CreateStagedInsert(doc.Collection, doc.Id, content, opId, doc.Cas, traceSpan.Item, options.Transcoder)
                    .CAF();
            }

            return await CreateStagedReplace(doc, content, opId, accessDeleted: doc.IsDeleted, parentSpan: traceSpan.Item, options.Transcoder)
                .CAF();
        }

        private void CheckForBinaryContent(object content, ITypeTranscoder? transcoder)
        {
            // we use the same logic as when we stage this in KV
            var wrapper = new JObjectContentWrapper(content, transcoder);
            if (wrapper.Flags.DataFormat == DataFormat.Binary)
            {
                throw ErrorBuilder.CreateError(this, ErrorClass.FailOther,
                    new FeatureNotAvailableException(
                        "Binary content isn't supported for transactional queries")).Build();
            }
        }

        private async Task<TransactionGetResult> ReplaceWithQuery(TransactionGetResult doc, object content,
            TransactionReplaceOptions options)
        {
            using var traceSpan = TraceSpan(parent: options.Span);

            var txdata = TxDataForReplaceAndRemove(doc);
            try
            {
                CheckForBinaryContent(content, options.Transcoder);
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

        private static object TxDataForReplaceAndRemove(TransactionGetResult doc)
        {
            var txdata = new Dictionary<string, object?>();
            txdata["kv"] = true;
            txdata["scas"] = doc.Cas.ToString(CultureInfo.InvariantCulture);
            if (doc.TxnMeta != null)
            {
                txdata["txnMeta"] = doc.TxnMeta;
            }

            return txdata;
        }

        private async Task SetAtrPendingIfFirstMutation(IRequestSpan? parentSpan)
        {
            if (_stagedMutations.IsEmpty)
            {
                await SetAtrPending(parentSpan).CAF();
            }
        }

        private async Task<TransactionGetResult> CreateStagedReplace(TransactionGetResult doc, object content,
            string opId, bool accessDeleted, IRequestSpan? parentSpan, ITypeTranscoder? transcoder)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            _ = _atr ?? throw new ArgumentNullException(nameof(_atr), "ATR should have already been initialized");
            try
            {
                try
                {
                    await _testHooks.BeforeStagedReplace(this, doc.Id).CAF();
                    var contentWrapper = new JObjectContentWrapper(content, transcoder);
                    bool isTombstone = doc.Cas == 0;
                    (var updatedCas, var mutationToken) =
                        await _docs.MutateStagedReplace(doc, contentWrapper, opId, _atr, accessDeleted).CAF();
                    Logger.LogDebug(
                        "{method} for {redactedId}, attemptId={attemptId}, preCase={preCas}, postCas={postCas}, accessDeleted={accessDeleted}",
                        nameof(CreateStagedReplace), Redactor.UserData(doc.Id), AttemptId, doc.Cas, updatedCas,
                        accessDeleted);
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
                        _stagedMutations.Add(new StagedMutation(doc, content, contentWrapper.Flags, StagedMutationType.Insert,
                            mutationToken));
                    }
                    else
                    {
                        // If doc is already in stagedMutations as a REPLACE, then overwrite it.
                        _stagedMutations.Add(
                            new StagedMutation(doc, content, contentWrapper.Flags, StagedMutationType.Replace, mutationToken));
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

        /*public Task<TransactionGetResult> InsertAsync(ICouchbaseCollection collection, string id,
            object content)
        {
            return InsertAsync(collection, id, content, TransactionInsertOptionsBuilder.Default);
        }*/

        public Task<TransactionGetResult> InsertAsync(ICouchbaseCollection collection, string id,
            object content, TransactionInsertOptionsBuilder? options = null)
        {
            var parentSpan = (options ??= TransactionInsertOptionsBuilder.Default).Build().Span;
            return _opWrapper.WrapOperationAsync(() => InsertWithKv(collection, id, content, options.Build()),
                () => InsertWithQuery(collection, id, content, options.Build()));
        }

        /// <summary>
        /// Insert a document.
        /// </summary>
        /// <param name="collection">The collection to insert the document into.</param>
        /// <param name="id">The ID of the new document.</param>
        /// <param name="content">The content of the new document.</param>
        /// <param name="parentSpan">The optional parent tracing span.</param>
        /// <returns>A <see cref="TransactionGetResult"/> representing the inserted document.</returns>
        public Task<TransactionGetResult> InsertAsync(ICouchbaseCollection collection, string id, object content,
            IRequestSpan? parentSpan)
        {
            var options = TransactionInsertOptionsBuilder.Default;
            if (parentSpan != null) options.Span(parentSpan);
            return _opWrapper.WrapOperationAsync(
                () => InsertWithKv(collection, id, content, options.Build()),
                () => InsertWithQuery(collection, id, content, options.Build()));
        }

        private async Task<TransactionGetResult> InsertWithKv(ICouchbaseCollection collection, string id,
            object content, TransactionInsertOptions options)
        {
            using var traceSpan = TraceSpan(parent: options.Span);
            using var logScope = Logger.BeginMethodScope();
            DoneCheck();
            CheckErrors();

            var stagedOld = _stagedMutations.Find(collection, id);
            if (stagedOld?.Type == StagedMutationType.Insert || stagedOld?.Type == StagedMutationType.Replace)
            {
                Logger.LogDebug("{id} already staged, raising DocumentExistsExceptin", id);
                throw new DocumentExistsException();
            }

            CheckExpiryAndThrow(id, hookPoint: DefaultTestHooks.HOOK_INSERT);

            await InitAtrAndSetPendingIfNeeded(collection, id, traceSpan.Item).CAF();

            var opId = Guid.NewGuid().ToString();
            if (stagedOld?.Type == StagedMutationType.Remove)
            {
                return await CreateStagedReplace(stagedOld.Doc, content, opId, true, traceSpan.Item, options.Transcoder).CAF();
            }

            return await CreateStagedInsert(collection, id, content, opId, parentSpan: traceSpan.Item, transcoder: options.Transcoder).CAF();
        }

        private async Task<TransactionGetResult> InsertWithQuery(ICouchbaseCollection collection, string id,
            object content, TransactionInsertOptions options)
        {
            using var traceSpan = TraceSpan(parent: options.Span);

            try
            {
                CheckForBinaryContent(content, options.Transcoder);
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
                    throw new DocumentExistsException();
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

        private async Task<TransactionGetResult> CreateStagedInsert(ICouchbaseCollection collection, string id,
            object content, string opId, ulong? cas = null, IRequestSpan? parentSpan = null,
            ITypeTranscoder? transcoder = null)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            try
            {
                bool isTombstone = cas == null;
                var result = await RepeatUntilSuccessOrThrow<TransactionGetResult?>(async () =>
                {
                    try
                    {
                        Logger.LogDebug("{method} for {redactedId}, attemptId={attemptId}, preCas={preCas}, opId={opId}", nameof(CreateStagedInsert),
                            Redactor.UserData(id), AttemptId, cas, opId);
                        // Check expiration again, since insert might be retried.
                        ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_CREATE_STAGED_INSERT, id);

                        await _testHooks.BeforeStagedInsert(this, id).CAF();
                        var contentWrapper = new JObjectContentWrapper(content, transcoder);
                        byte[]? byteContent = contentWrapper.ContentAs<byte[]>();
                        if (byteContent == null)
                            throw new InvalidArgumentException("couldn't convert content to byte[]");
                        (var updatedCas, var mutationToken) =
                            await _docs.MutateStagedInsert(collection, id, contentWrapper, opId, _atr!, cas).CAF();
                        Logger.LogDebug(
                            "{method} for {redactedId}, attemptId={attemptId}, preCas={preCas}, postCas={postCas}, opId={opId}",
                            nameof(CreateStagedInsert), Redactor.UserData(id), AttemptId, cas, updatedCas, opId);
                        _ = _atr ?? throw new ArgumentNullException(nameof(_atr),
                            "ATR should have already been initialized");
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

                        var stagedMutation = new StagedMutation(getResult, byteContent, contentWrapper.Flags, StagedMutationType.Insert,
                            mutationToken);
                        _stagedMutations.Add(stagedMutation);

                        return (RepeatAction.NoRepeat, getResult);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogDebug("{method} got {ex} attempting to write staged insert",
                            nameof(CreateStagedInsert), ex.Message);
                        var triaged = _triage.TriageCreateStagedInsertErrors(ex, _expirationOvertimeMode);
                        switch (triaged.ec)
                        {
                            case ErrorClass.FailExpiry:
                                _expirationOvertimeMode = true;
                                throw _triage.AssertNotNull(triaged, ex);
                            case ErrorClass.FailAmbiguous:
                                Logger.LogDebug("{method} got an ambiguous exception for {id}, with delay...", nameof(CreateStagedInsert), Redactor.UserData(id));
                                return (RepeatAction.RepeatWithDelay, null);
                            case ErrorClass.FailDocNotFound:
                                // MutateIn can return this when there is a tombstone written concurrently, it seems
                                Logger.LogDebug("{method} got DocNotFound", nameof(CreateStagedInsert));
                                throw ErrorBuilder.CreateError(this, ErrorClass.FailDocAlreadyExists,
                                    new DocumentExistsException()).Build();
                            case ErrorClass.FailCasMismatch:
                            case ErrorClass.FailDocAlreadyExists:
                                TransactionGetResult? docAlreadyExistsResult = null;
                                var repeatAction = await RepeatUntilSuccessOrThrow<RepeatAction>(async () =>
                                {
                                    try
                                    {
                                        Logger.LogDebug(
                                            "{method}.HandleDocExists for {redactedId}, attemptId={attemptId}, preCas={preCas}",
                                            nameof(CreateStagedInsert), Redactor.UserData(id),
                                            AttemptId, 0);
                                        await _testHooks
                                            .BeforeGetDocInExistsDuringStagedInsert(this, id).CAF();
                                        var docWithMeta = await _docs
                                            .LookupDocumentAsync(collection, id,
                                                fullDocument: false).CAF();
                                        await ForwardCompatibility.Check(this,
                                                ForwardCompatibility.WriteWriteConflictInsertingGet,
                                                docWithMeta?.TransactionXattrs
                                                    ?.ForwardCompatibility)
                                            .CAF();
                                        if (docWithMeta?.TransactionXattrs?.Id?.AttemptId ==
                                            AttemptId)
                                        {
                                            if (docWithMeta?.TransactionXattrs?.Id.OperationId ==
                                                opId)
                                            {
                                                Logger.LogDebug(
                                                    "update cas as we are only resolving ambiguity");
                                                docAlreadyExistsResult =
                                                    docWithMeta.GetPostTransactionResult();
                                                var stagedMutation =
                                                    new StagedMutation(docAlreadyExistsResult,
                                                        content, docAlreadyExistsResult.Flags, StagedMutationType.Insert);
                                                _stagedMutations.Add(stagedMutation);
                                                return (RepeatAction.NoRepeat,
                                                    RepeatAction.NoRepeat);
                                            }

                                            Logger.LogWarning("concurrent insert of #{redacted_id}", Redactor.UserData(id));
                                            throw CreateError(this, ErrorClass.FailOther,
                                                new DocumentExistsException()).Build();
                                        }

                                        var docInATransaction =
                                            docWithMeta?.TransactionXattrs?.Id?.Transactionid !=
                                            null;
                                        isTombstone = docWithMeta?.IsDeleted == true;
                                        if (!docInATransaction)
                                        {
                                            if (!isTombstone)
                                                throw new DocumentExistsException(
                                                    $"Document with key {id} already exists");

                                            // If the doc is a tombstone and not in any transaction
                                            // -> It’s ok to go ahead and overwrite.
                                            // Perform this algorithm (createStagedInsert) from the top with cas=the cas from the get.
                                            cas = docWithMeta!.Cas;

                                            return (RepeatAction.NoRepeat,
                                                RepeatAction.RepeatNoDelay);
                                        }
                                        // Else if the doc is not in a transaction
                                        // -> Raise Error(FAIL_DOC_ALREADY_EXISTS, cause=DocumentExistsException).
                                        // There is logic further up the stack that handles this by fast-failing the transaction.
                                        // TODO: BF-CBD-3787
                                        var operationType = docWithMeta?.TransactionXattrs
                                            ?.Operation?.Type;
                                        if (operationType != "insert")
                                        {
                                            Logger.LogWarning(
                                                "BF-CBD-3787 FAIL_DOC_ALREADY_EXISTS here because type = {operationType}",
                                                operationType);
                                            throw new DocumentExistsException($"Document with key {id} already exists");
                                        }

                                        // Else call the CheckWriteWriteConflict logic, which conveniently does everything we need to handle the above cases.
                                        var getResult = docWithMeta!.GetPostTransactionResult();
                                        await CheckWriteWriteConflict(getResult,
                                            ForwardCompatibility.WriteWriteConflictInserting,
                                            traceSpan.Item).CAF();

                                        // BF-CBD-3787: If the document is a staged insert but also is not a tombstone (e.g. it is from protocol 1.0), it must be deleted first
                                        if (operationType == "insert" && !isTombstone)
                                        {
                                            try
                                            {
                                                await _testHooks
                                                    .BeforeOverwritingStagedInsertRemoval(this, id)
                                                    .CAF();
                                                await _docs.UnstageRemove(collection, id,
                                                    getResult.Cas).CAF();
                                            }
                                            catch (Exception err)
                                            {
                                                var ec = err.Classify();
                                                switch (ec)
                                                {
                                                    case ErrorClass.FailDocNotFound:
                                                    case ErrorClass.FailCasMismatch:
                                                        throw CreateError(this, ec, err)
                                                            .RetryTransaction().Build();
                                                    default:
                                                        throw CreateError(this, ec, err).Build();
                                                }
                                            }

                                            // hack workaround for NCBC-2944
                                            // Supposed to "retry this (CreateStagedInsert) algorithm with the cas from the Remove", but we don't have a Cas from the Remove.
                                            // Instead, we just trigger a retry of the entire transaction, since this is such an edge case.
                                            throw CreateError(this, ErrorClass.FailDocAlreadyExists,
                                                    ex)
                                                .RetryTransaction().Build();
                                        }

                                        // If this logic succeeds, we are ok to overwrite the doc.
                                        // Perform this algorithm (createStagedInsert) from the top, with cas=the cas from the get.
                                        cas = docWithMeta.Cas;
                                        return (RepeatAction.NoRepeat, RepeatAction.RepeatNoDelay);

                                    }
                                    catch(DocumentExistsException)
                                    {
                                        Logger.LogInformation(
                                            "CreateStagedInsert raising ignorable DocumentExistsException");
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        var triagedDocExists = _triage.TriageDocExistsOnStagedInsertErrors(ex);
                                        throw _triage.AssertNotNull(triagedDocExists, ex);
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
                        await _testHooks.BeforeAtrPending(this).CAF();
                        var t1 = _overallContext.StartTime;
                        var t2 = DateTimeOffset.UtcNow;
                        var tElapsed = t2 - t1;
                        var tc = _config.ExpirationTime;
                        var tRemaining = tc - tElapsed;
                        var exp = (ulong)Math.Max(Math.Min(tRemaining.TotalMilliseconds, tc.TotalMilliseconds), 0);
                        await _atr.MutateAtrPending(exp, docDurability).CAF();
                        Logger?.LogDebug(
                            $"{nameof(SetAtrPending)} for {Redactor.UserData(_atr.FullPath)} (attempt={AttemptId})");
                        await _testHooks.AfterAtrPending(this).CAF();
                        AttemptState = AttemptStates.PENDING;
                        return RepeatAction.NoRepeat;
                    }
                    catch (Exception ex)
                    {
                        var triaged = _triage.TriageSetAtrPendingErrors(ex, _expirationOvertimeMode);
                        Logger.LogWarning("Failed with {ec} in {method}: {reason}", triaged.ec, nameof(SetAtrPending),
                            ex.Message);
                        switch (triaged.ec)
                        {
                            case ErrorClass.FailExpiry:
                                _expirationOvertimeMode = true;
                                break;
                            case ErrorClass.FailTransient:
                                throw _triage.AssertNotNull(triaged, ex);
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
        {
            return _opWrapper.WrapOperationAsync(() => RemoveWithKv(doc, parentSpan),
                () => RemoveWithQuery(doc, parentSpan));
        }

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
            await InitAtrAndSetPendingIfNeeded(doc.Collection, doc.Id, traceSpan.Item).CAF();
            await CreateStagedRemove(doc, traceSpan.Item).CAF();
        }

        private async Task RemoveWithQuery(TransactionGetResult doc, IRequestSpan? parentSpan)
        {
            _ = doc ?? throw new ArgumentNullException(nameof(doc));
            using var traceSpan = TraceSpan(parent: parentSpan);
            var txdata = TxDataForReplaceAndRemove(doc);

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
                    Logger?.LogDebug(
                        $"{nameof(CreateStagedRemove)} for {Redactor.UserData(doc.Id)}, attemptId={AttemptId}, preCas={doc.Cas}, postCas={updatedCas}");
                    await _testHooks.AfterStagedRemoveComplete(this, doc.Id).CAF();

                    doc.Cas = updatedCas;


                    var stagedRemove = new StagedMutation(doc, null, null,
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

            switch (AttemptState)
            {
                case AttemptStates.NOTHING_WRITTEN:
                case AttemptStates.PENDING:
                    await CommitAsync(parentSpan).CAF();
                    break;
            }
        }

        internal async Task CommitAsync(IRequestSpan? parentSpan = null)
        {
            await _opWrapper
                .WaitOnTasksThenPerformUnderLockAsync(() => CommitAsyncLocked(parentSpan)).CAF();
        }
        private async Task CommitAsyncLocked(IRequestSpan? parentSpan)
        {
            if (!_previousErrors.IsEmpty)
            {
                _triage.ThrowIfCommitWithPreviousErrors(_previousErrors.Values);
            }
            // We should disallow another commit or rollback
            StateFlags.SetFlags(
                StateFlags.BehaviorFlags.AppRollbackNotAllowed | StateFlags.BehaviorFlags.CommitNotAllowed, 0);
            if (_opWrapper.IsQueryMode)
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
                AttemptState = AttemptStates.COMPLETED;
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
            if (HasExpiredClientSide(null, DefaultTestHooks.HOOK_ATR_COMPLETE) && !_expirationOvertimeMode)
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
                Logger?.LogDebug(
                    $"{nameof(SetAtrComplete)} for {Redactor.UserData(_atr.FullPath)} (attempt={AttemptId})");
                await _testHooks.AfterAtrComplete(this).CAF();
                AttemptState = AttemptStates.COMPLETED;
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
            var taskLimiter = new TaskLimiter(_unstagingConcurrency);
            foreach (var sm in allStagedMutations)
            {
                taskLimiter.Run(sm, UnstageDoc);
            }

            await taskLimiter.WaitAllAsync().CAF();
        }

        private async Task UnstageDoc(StagedMutation sm)
        {
                (var cas, var content) = await FetchIfNeededBeforeUnstage(sm).CAF();
                switch (sm.Type)
                {
                    case StagedMutationType.Remove:
                        await UnstageRemove(sm).CAF();
                        break;
                    case StagedMutationType.Insert:
                        await UnstageInsertOrReplace(sm, cas, content, insertMode: true,
                                ambiguityResolutionMode: false)
                            .CAF();
                        break;
                    case StagedMutationType.Replace:
                        await UnstageInsertOrReplace(sm, cas, content, insertMode: false,
                            ambiguityResolutionMode: false).CAF();
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Cannot un-stage transaction mutation of type {sm.Type}");
                }
        }

        private async Task UnstageRemove(StagedMutation sm, bool ambiguityResolutionMode = false,
            IRequestSpan? parentSpan = null)
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
                    if (!_expirationOvertimeMode && HasExpiredClientSide(sm.Doc.Id, DefaultTestHooks.HOOK_REMOVE_DOC))
                    {
                        _expirationOvertimeMode = true;
                    }

                    await _docs.UnstageRemove(sm.Doc.Collection, sm.Doc.Id).CAF();
                    Logger.LogDebug("Unstaged RemoveAsync successfully for {redactedId)} (retryCount={retryCount}",
                        Redactor.UserData(sm.Doc.FullyQualifiedId), retryCount);
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



        private async Task UnstageInsertOrReplace(StagedMutation sm, ulong cas, object content, bool insertMode = false,
            bool ambiguityResolutionMode = false, IRequestSpan? parentSpan = null)
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


                    await _testHooks.BeforeDocCommitted(this, sm.Doc.Id).CAF();
                    var (updatedCas, mutationToken) = await _docs
                        .UnstageInsertOrReplace(sm.Doc.Collection, sm.Doc.Id, cas, content, insertMode, sm.Flags ?? new Flags()).CAF();
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
                            RepeatAction returnVal;
                            (returnVal, cas) = await HandleDocChangedDuringCommit(sm, cas).CAF();
                            if (returnVal !=  RepeatAction.NoRepeat)
                                ambiguityResolutionMode = true;
                            return returnVal;
                        case ErrorClass.FailDocNotFound:
                            // TODO: publish IllegalDocumentState event to the application.
                            Logger?.LogError("IllegalDocumentState: " + triaged.ec);
                            insertMode = true;
                            return RepeatAction.RepeatWithDelay;
                        case ErrorClass.FailDocAlreadyExists:
                            // if resolving ambiguity, or if this is a replace, then this is ok
                            if (ambiguityResolutionMode || !insertMode)
                                return RepeatAction.NoRepeat;
                            if (_docs.SupportsReplaceBodyWithXattr(sm.Doc.Collection))
                            {
                                throw _triage.AssertNotNull(triaged, ex);
                            }
                            // now consider it a replace
                            insertMode = false;
                            cas = 0;
                            return RepeatAction.RepeatWithDelay;
                    }

                    throw _triage.AssertNotNull(triaged, ex);
                }
            }).CAF();
        }

        private async Task<(RepeatAction, ulong)> HandleDocChangedDuringCommit(StagedMutation sm, ulong cas)
        {
            Logger.LogDebug("handling doc changed during commit");
            if (HasExpiredClientSide(sm.Doc.Id, DefaultTestHooks.HOOK_BEFORE_DOC_CHANGED_DURING_COMMIT))
            {
                throw CreateError(this, ErrorClass.FailExpiry)
                    .DoNotRollbackAttempt()
                    .Cause(new AttemptExpiredException(this,
                        "Commit expired in HandleDocChangedDuringCommit"))
                    .RaiseException(TransactionOperationFailedException.FinalError.TransactionFailedPostCommit)
                    .Build();
            }
            try
            {
                await _testHooks.BeforeDocChangedDuringCommit(this, sm.Doc.Id).CAF();
                // Same txn/attempt, so let's just retry with the new cas
                cas = 0;
                return (RepeatAction.RepeatNoDelay, cas);
            } catch (Exception ex)
            {
                var triaged = _triage.TriageUnstageInsertOrReplaceErrors(ex, _expirationOvertimeMode);
                Logger.LogDebug("handling doc changed during commit got {ec}", triaged.ec);
                return triaged.ec switch
                {
                    ErrorClass.FailTransient => (RepeatAction.RepeatWithDelay, cas),
                    ErrorClass.TransactionOperationFailed => throw new
                        TransactionOperationFailedException(ErrorClass.FailCasMismatch, false, true,
                            ex, TransactionOperationFailedException.FinalError.TransactionFailed),
                    _ => throw _triage.AssertNotNull(triaged, ex)
                };
            }
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
                    await _testHooks.BeforeAtrCommit(this).CAF();
                    await _atr.MutateAtrCommit(_stagedMutations.ToList()).CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})", nameof(SetAtrCommit),
                        Redactor.UserData(_atr.FullPath), AttemptId);
                    await _testHooks.AfterAtrCommit(this).CAF();
                    AttemptState = AttemptStates.COMMITTED;
                    return RepeatAction.NoRepeat;
                }
                catch (Exception err)
                {
                    var ec = err.Classify();
                    Logger.LogWarning("Failed attempt at committing due to {ec}", ec);
                    var cause = err;
                    var rollback = true;
                    switch (ec)
                    {
                        case ErrorClass.FailExpiry when ambiguityResolutionMode:
                            throw Error(ec,
                                new AttemptExpiredException(this,
                                    "Attempt expired ambiguously in " + nameof(SetAtrCommit)), rollback: false,
                                raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        case ErrorClass.FailExpiry:
                            throw Error(ec,
                                new AttemptExpiredException(this, "Attempt expired in " + nameof(SetAtrCommit)),
                                rollback: false,
                                raise: TransactionOperationFailedException.FinalError.TransactionExpired);
                        case ErrorClass.FailAmbiguous:
                            ambiguityResolutionMode = true;
                            return RepeatAction.RepeatWithDelay;
                        case ErrorClass.FailHard when ambiguityResolutionMode:
                            throw Error(ec, err, rollback: false,
                                raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        case ErrorClass.FailHard:
                            throw Error(ec, err, rollback: false);
                        case ErrorClass.FailTransient when ambiguityResolutionMode:
                            // We haven't yet reached clarity on what state this attempt is in, so we can’t rollback or continue.
                            return RepeatAction.RepeatWithDelay;
                        case ErrorClass.FailTransient:
                            throw Error(ec, err, retry: true);
                        case ErrorClass.FailPathAlreadyExists:
                        {
                            var repeatAction = await ResolveSetAtrCommitAmbiguity(traceSpan.Item).CAF();
                            if (repeatAction != RepeatAction.NoRepeat)
                            {
                                ambiguityResolutionMode = false;
                            }

                            return repeatAction;
                        }
                        case ErrorClass.FailDocNotFound:
                            cause = new ActiveTransactionRecordNotFoundException();
                            rollback = false;
                            break;
                        case ErrorClass.FailPathNotFound:
                            cause = new ActiveTransactionRecordEntryNotFoundException();
                            rollback = false;
                            break;
                        case ErrorClass.FailAtrFull:
                            cause = new ActiveTransactionRecordsFullException(this, "Full ATR in SetAtrCommit");
                            rollback = false;
                            break;
                    }

                    if (ambiguityResolutionMode)
                    {
                        // we were unable to attain clarity
                        throw Error(ec, cause, rollback: false,
                            raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                    }

                    throw Error(ec, cause, rollback: rollback);
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
                    await _testHooks.BeforeAtrCommitAmbiguityResolution(this).CAF();
                    refreshedStatus = await _atr!.LookupAtrState().CAF();

                }
                catch (Exception exAmbiguity)
                {
                    var ec = exAmbiguity.Classify();
                    switch (ec)
                    {
                        case ErrorClass.FailExpiry:
                            throw Error(ec,
                                new AttemptExpiredException(this, "expired resolving commit ambiguity", exAmbiguity),
                                rollback: false,
                                raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
                        case ErrorClass.FailHard:
                            throw Error(ec, exAmbiguity, rollback: false,
                                raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
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

                            throw Error(ec, cause, rollback: false,
                                raise: TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous);
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
                        throw Error(ErrorClass.FailOther,
                            new InvalidOperationException("Unknown ATR state: " + refreshedStatus), rollback: false);
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
                    await _testHooks.BeforeAtrAborted(this).CAF();
                    await _atr!.MutateAtrAborted(_stagedMutations.ToList()).CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})", nameof(SetAtrAborted),
                        Redactor.UserData(_atr.FullPath), AttemptId);
                    await _testHooks.AfterAtrAborted(this).CAF();
                    AttemptState = AttemptStates.ABORTED;
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

                    (ErrorClass ec, TransactionOperationFailedException? toThrow) =
                        _triage.TriageSetAtrAbortedErrors(ex);
                    switch (ec)
                    {
                        case ErrorClass.FailExpiry:
                            _expirationOvertimeMode = true;
                            return RepeatAction.RepeatWithBackoff;
                        case ErrorClass.FailPathNotFound:
                        case ErrorClass.FailDocNotFound:
                        case ErrorClass.FailAtrFull:
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

        private async Task SetAtrRolledBack(IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#SetATRRolledBack
            await RepeatUntilSuccessOrThrow(async () =>
            {
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ATR_ROLLBACK_COMPLETE);
                    await _testHooks.BeforeAtrRolledBack(this).CAF();
                    await _atr!.MutateAtrRolledBack().CAF();
                    Logger.LogDebug("{method} for {atr} (attempt={attemptId})",
                        nameof(SetAtrRolledBack),
                        Redactor.UserData(_atr.FullPath),
                        AttemptId);
                    await _testHooks.AfterAtrRolledBack(this).CAF();
                    AttemptState = AttemptStates.ROLLED_BACK;
                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    BailoutIfInOvertime(rollback: false);

                    (ErrorClass ec, TransactionOperationFailedException? toThrow) =
                        _triage.TriageSetAtrRolledBackErrors(ex);
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

        internal Task RollbackAsync(IRequestSpan? parentSpan = null)
        {
            // check state flags here...
            if (StateFlags.IsFlagSet(StateFlags.BehaviorFlags.AppRollbackNotAllowed))
            {
                throw CreateError(this, ErrorClass.FailOther, new RollbackNotPermittedException())
                .DoNotRollbackAttempt()
                .Build();
            }
            return RollbackInternal(true, parentSpan);
        }

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
        public Task<IQueryResult<T>> QueryAsync<T>(string statement, TransactionQueryConfigBuilder? config = null,
            IScope? scope = null, IRequestSpan? parentSpan = null)
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
        public Task<IQueryResult<T>> QueryAsync<T>(string statement, TransactionQueryOptions options,
            IScope? scope = null, IRequestSpan? parentSpan = null)
            => QueryAsync<T>(statement, options, false, scope, parentSpan);

        internal async Task<IQueryResult<T>> QueryAsync<T>(string statement, TransactionQueryOptions options,
            bool txImplicit, IScope? scope = null, IRequestSpan? parentSpan = null)
        {
            var traceSpan = TraceSpan(parent: parentSpan);
            long fixmeStatementId = 0;
            var results = await QueryWrapper<T>(
                statementId: fixmeStatementId,
                scope: scope,
                statement: statement,
                options: options.Build(txImplicit),
                hookPoint: DefaultTestHooks.HOOK_QUERY,
                parentSpan: traceSpan.Item,
                txImplicit: txImplicit
            ).CAF();

            return results;
        }

        private bool IsDone { get; set; }

        internal async Task RollbackInternal(bool isAppRollback, IRequestSpan? parentSpan)
        {
            await _opWrapper.WaitOnTasksThenPerformUnderLockAsync(() => RollbackInternalLocked(isAppRollback, parentSpan)).CAF();
        }
        private async Task RollbackInternalLocked(bool isAppRollback, IRequestSpan? parentSpan)
        {
            if (isAppRollback && StateFlags.IsFlagSet(StateFlags.BehaviorFlags.AppRollbackNotAllowed))
            {
                throw CreateError(this, ErrorClass.FailOther, new RollbackNotPermittedException()).Build();
            }
            // No more commit or rollback...
            StateFlags.SetFlags(
                StateFlags.BehaviorFlags.AppRollbackNotAllowed | StateFlags.BehaviorFlags.CommitNotAllowed, 0);

            if (_opWrapper.IsQueryMode)
            {
                await RollbackWithQuery(isAppRollback, parentSpan).CAF();
            }
            else
            {
                await RollbackWithKv(isAppRollback, parentSpan).CAF();
            }
            // if we make it here, lets be sure to create an exception if none was raised.  This is
            // strictly for the testing - it expects an exception when rollback is called.  We could
            // trigger a rollback by raising an exception, but _that_ exception will be the one that
            // gets raised which breaks a couple other tests.  Non-internal users cannot get here without
            // an exception, so this _only_ is for tests.
            if (this.StateFlags.GetFinalError() ==
                TransactionOperationFailedException.FinalError.None)
            {
                throw ErrorBuilder.CreateError(this, ErrorClass.FailOther).DoNotRollbackAttempt().Build();
            }
        }

        internal async Task RollbackWithKv(bool isAppRollback, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);

            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#rollbackInternal
            if (!_expirationOvertimeMode)
            {
                if (HasExpiredClientSide(null, hookPoint: DefaultTestHooks.HOOK_ROLLBACK))
                {
                    _expirationOvertimeMode = true;
                }
            }

            if (AttemptState == AttemptStates.NOTHING_WRITTEN)
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
            var sem = new SemaphoreSlim(_unstagingConcurrency);
            var tasks = allMutations.Select(async sm =>
            {
                try
                {
                    await sem.WaitAsync().CAF();
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
                            throw new InvalidOperationException(sm.Type +
                                                                " is not a supported mutation type for rollback.");

                    }
                }
                finally
                {
                    sem.Release();
                }
            });
            await  Task.WhenAll(tasks).CAF();
            await SetAtrRolledBack(traceSpan.Item).CAF();
        }

        internal async Task RollbackWithQuery(bool isAppRollback, IRequestSpan? parentSpan)
        {
            var traceSpan = TraceSpan(parent: parentSpan);
            try
            {
                Logger.LogDebug("Attempting RollbackWithQuery...");

                var queryOptions = NonStreamingQuery();
                _ = await QueryWrapper<object>(0, _queryContextScope, "ROLLBACK", queryOptions,
                    hookPoint: DefaultTestHooks.HOOK_QUERY_ROLLBACK,
                    parentSpan: traceSpan.Item,
                    existingErrorCheck: false).CAF();
                Logger.LogDebug("RollbackWithQuery successful");
                AttemptState = AttemptStates.ROLLED_BACK;
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
                    AttemptState = AttemptStates.ROLLED_BACK;
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
                Logger.LogDebug("[{attemptId}] rolling back staged insert for {redactedId}", AttemptId,
                    Redactor.UserData(sm.Doc.FullyQualifiedId));
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_DELETE_INSERTED, sm.Doc.Id);
                    await _testHooks.BeforeRollbackDeleteInserted(this, sm.Doc.Id).CAF();
                    await _docs.ClearTransactionMetadata(sm.Doc.Collection, sm.Doc.Id, sm.Doc.Cas, true).CAF();
                    Logger.LogDebug("Rolled back staged {type} for {redactedId}", sm.Type,
                        Redactor.UserData(sm.Doc.Id));
                    await _testHooks.AfterRollbackDeleteInserted(this, sm.Doc.Id).CAF();
                    return RepeatAction.NoRepeat;
                }
                catch (Exception ex)
                {
                    BailoutIfInOvertime(rollback: false);

                    (ErrorClass ec, TransactionOperationFailedException? toThrow) =
                        _triage.TriageRollbackStagedInsertErrors(ex);
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
                Logger.LogDebug("[{attemptId}] rolling back staged replace or remove for {redactedId}", AttemptId,
                    Redactor.UserData(sm.Doc.FullyQualifiedId));
                try
                {
                    ErrorIfExpiredAndNotInExpiryOvertimeMode(DefaultTestHooks.HOOK_ROLLBACK_DOC, sm.Doc.Id);
                    await _testHooks.BeforeDocRolledBack(this, sm.Doc.Id).CAF();
                    await _docs.ClearTransactionMetadata(sm.Doc.Collection, sm.Doc.Id, sm.Doc.Cas, sm.Doc.IsDeleted)
                        .CAF();
                    Logger.LogDebug("Rolled back staged {type} for {redactedId}", sm.Type,
                        Redactor.UserData(sm.Doc.Id));
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
            var isDoneState = !(AttemptState == AttemptStates.NOTHING_WRITTEN || AttemptState == AttemptStates.PENDING);
            if (IsDone || isDoneState)
            {
                throw CreateError(this, ErrorClass.FailOther)
                    .Cause(new InvalidOperationException(
                        "Cannot perform operations after a transaction has been committed or rolled back."))
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

        private async Task InitAtrAndSetPendingIfNeeded(ICouchbaseCollection collection, string id, IRequestSpan? parentSpan)
        {
            await InitAtrIfNeeded(collection, id, parentSpan).CAF();
        }

        private async Task InitAtrIfNeeded(ICouchbaseCollection collection, string id, IRequestSpan? parentSpan)
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            var testHookAtrId = await _testHooks.AtrIdForVBucket(this, AtrIds.GetVBucketId(id)).CAF();
            var atrId = AtrIds.GetAtrId(id);
            // This is called with collection being the collection of the first document that we are modifying
            // in the txn.  We default to default collection in the bucket of this document, unless the
            // metadata collection is specified in the config.
            var atrCollection = collection.Scope.Bucket.DefaultCollection();

            // quick check without taking a lock
            if (_atr != null) return;
            await _attemptLock.WaitAsync().CAF();
            try
            {
                // TODO: AtrRepository should be built via factory to actually support mocking.
                // second check on _atr just in case we are trying this in several threads.  Like Highlanders,
                // only one can win.
                if (_atr == null)
                {
                    if (_config.MetadataCollection != null)
                    {
                        atrCollection = await _config.MetadataCollection!.ToCouchbaseCollection(_cluster).CAF();
                    }
                    _atr = new AtrRepository(
                        attemptId: AttemptId,
                        overallContext: _overallContext,
                        atrCollection: atrCollection,
                        atrId: atrId,
                        atrDurability: _config.DurabilityLevel,
                        loggerFactory: _loggerFactory,
                        testHookAtrId: testHookAtrId);

                    // Inform the LostTransactionCleanup that we may need to clean this collection
                    if (_cluster.Transactions._lostTransactionsCleanup is LostTransactionManager lostTransactionManager)
                    {
                        lostTransactionManager.AddCollection(atrCollection);
                    }
                    // well, if the atr wasn't set, this is always the first mutation.  We call this inside the lock
                    // so we only try once when heavily contending.
                    await SetAtrPendingIfFirstMutation(parentSpan).CAF();
                }
            }
            finally
            {
                _attemptLock.Release();
            }
        }

        internal void CheckExpiryAndThrow(string? docId, string hookPoint)
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

        private void ErrorIfExpiredAndNotInExpiryOvertimeMode(string hookPoint, string? docId = null,
            [CallerMemberName] string caller = "")
        {
            if (_expirationOvertimeMode)
            {
                Logger.LogInformation(
                    "[{attemptId}] not doing expiry check in {hookPoint}/{caller} as already in expiry overtime mode.",
                    AttemptId, hookPoint, caller);
                return;
            }

            if (HasExpiredClientSide(docId, hookPoint))
            {
                Logger.LogInformation("[{attemptId}] has expired in stage {hookPoint}/{caller}", AttemptId, hookPoint,
                    caller);
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
                    Logger.LogInformation("expired in stage {hookPoint} / attemptId = {attemptId}", hookPoint,
                        AttemptId);
                }

                if (hook)
                {
                    Logger.LogInformation("fake expiry in stage {hookPoint} / attemptId = {attemptId}", hookPoint,
                        AttemptId);
                }

                return over || hook;
            }
            catch
            {
                Logger.LogDebug("fake expiry due to throw in stage {hookPoint}", hookPoint);
                throw;
            }
        }

        internal async Task CheckWriteWriteConflict(TransactionGetResult gr, string interactionPoint,
            IRequestSpan? parentSpan)
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
                Logger.LogDebug("{method}@{interactionPoint} for {redactedId}, attempt={attemptId}", method,
                    interactionPoint, redactedId, AttemptId);
                await ForwardCompatibility.Check(this, interactionPoint, gr.TransactionXattrs?.ForwardCompatibility)
                    .CAF();
                var otherAtrFromDocMeta = gr.TransactionXattrs?.AtrRef;
                if (otherAtrFromDocMeta == null)
                {
                    Logger.LogDebug("{method} no other txn for {redactedId}, attempt={attemptId}", method, redactedId,
                        AttemptId);

                    // If gr has no transaction Metadata, it’s fine to proceed.
                    return RepeatAction.NoRepeat;
                }

                if (gr.TransactionXattrs?.Id?.Transactionid == _overallContext.TransactionId)
                {
                    Logger.LogDebug("{method} same txn for {redactedId}, attempt={attemptId}", method, redactedId,
                        AttemptId);
                    return RepeatAction.NoRepeat;
                }

                // Do a lookupIn call to fetch the ATR entry for B.
                ICouchbaseCollection? otherAtrCollection = null;
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
                    ? await AtrRepository.FindEntryForTransaction(otherAtrCollection, txn.AtrRef!.Id!,
                        txn.Id!.AttemptId!, _config.KeyValueTimeout).CAF()
                    : await _atr.FindEntryForTransaction(otherAtrCollection, txn.AtrRef!.Id!, txn.Id?.AttemptId).CAF();

                if (otherAtr == null)
                {
                    // cleanup occurred, OK to proceed.
                    Logger.LogDebug("{method} cleanup occurred on other ATR for {redactedId}, attempt={attemptId}",
                        method, redactedId, AttemptId);
                    return RepeatAction.NoRepeat;
                }

                Logger.LogDebug("[{attemptId}] OtherATR.TransactionId = {otherAtrId} in state {otherAtrState}",
                    AttemptId, otherAtr.TransactionId, otherAtr.State);

                await ForwardCompatibility.Check(this, ForwardCompatibility.WriteWriteConflictReadingAtr,
                    otherAtr.ForwardCompatibility).CAF();

                if (otherAtr.State == AttemptStates.COMPLETED || otherAtr.State == AttemptStates.ROLLED_BACK)
                {
                    Logger.LogInformation(
                        "[{attemptId}] ATR entry state of {attemptState} indicates we can proceed to overwrite",
                        AttemptId, otherAtr.State);

                    // ok to proceed
                    Logger.LogDebug("{method} other ATR is {otherAtrState} for {redactedId}, attempt={attemptId}",
                        method, otherAtr.State, redactedId, AttemptId);
                    return RepeatAction.NoRepeat;
                }

                if (sw.Elapsed > WriteWriteConflictTimeLimit)
                {
                    Logger.LogWarning(
                        "{method} CONFLICT DETECTED. Other ATR TransactionId={otherAtrTransactionid} is {otherAtrState} for document {redactedId}, thisAttempt={transactionId}/{attemptId}",
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
                    Logger.LogDebug("{elapsed}ms elapsed in {method}", sw.Elapsed.TotalMilliseconds,
                        nameof(CheckWriteWriteConflict));
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
            object? txdata = null,
            bool txImplicit = false
        )
        {
            Logger.LogDebug("Executing QueryWrapper with txdata={txdata}", txdata);
            if (!isBeginWork)
            {
                return await _opWrapper.WrapQueryOperationAsync(() => QueryWrapperLocked<T>(statementId, scope,
                    statement, options,
                    hookPoint, parentSpan, isBeginWork, existingErrorCheck, txdata, txImplicit)).CAF();
            }
            return await QueryWrapperLocked<T>(statementId, scope, statement, options,
            hookPoint, parentSpan, isBeginWork, existingErrorCheck, txdata, txImplicit).CAF();
        }

        private async Task<IQueryResult<T>> QueryWrapperLocked<T>(
                long statementId,
                IScope? scope,
                string statement,
                QueryOptions options,
                string hookPoint,
                IRequestSpan? parentSpan,
                bool isBeginWork = false,
                bool existingErrorCheck = true,
                object? txdata = null,
                bool txImplicit = false
            )
        {
            using var traceSpan = TraceSpan(parent: parentSpan);
            traceSpan.Item?.SetAttribute("db.statement", statement)
                          ?.SetAttribute("db.couchbase.transactions.tximplicit", txImplicit);

            Logger.LogDebug("[{attemptId}] Executing Query: {hookPoint}: {txdata}", AttemptId, hookPoint, Redactor.UserData(txdata?.ToString()));

            if (!_opWrapper.IsQueryMode && !isBeginWork)
            {
                if(!txImplicit)
                {
                    await QueryBeginWork(traceSpan?.Item, scope).CAF();
                }

                _opWrapper.SetQueryMode();
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
                var txdataSingleQuery = CreateBeginWorkTxData().ToDictionary();
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

        private static string DurabilityLevelToString(DurabilityLevel durabilityLevel)
        {
            return durabilityLevel switch
            {
                DurabilityLevel.None => "NONE",
                DurabilityLevel.Majority => "MAJORITY",
                DurabilityLevel.MajorityAndPersistToActive => "MAJORITY_AND_PERSIST_TO_ACTIVE",
                DurabilityLevel.PersistToMajority => "PERSIST_TO_MAJORITY",
                _ => durabilityLevel.ToString()
            };
        }

        private QueryTxData CreateBeginWorkTxData()
        {
            var state =
                new TxDataState((long)_overallContext.RemainingUntilExpiration.TotalMilliseconds);
            var txConfig = new TxDataReportedConfig(
                (long?)_config?.KeyValueTimeout?.TotalMilliseconds ?? 10_000, AtrIds.NumAtrs,
                DurabilityLevelToString(_effectiveDurabilityLevel));

            var mutations = _stagedMutations?.ToList().Select(sm => sm.AsTxData()) ??
                            Array.Empty<TxDataMutation>();
            var txid = new CompositeId()
            {
                Transactionid = _overallContext.TransactionId,
                AttemptId = AttemptId
            };

            var atrRef = _atr?.AtrRef;
            if (atrRef == null && _config?.MetadataCollection != null)
            {
                atrRef = new AtrRef()
                {
                    BucketName = _config.MetadataCollection.BucketName,
                    ScopeName = _config.MetadataCollection.ScopeName,
                    CollectionName = _config?.MetadataCollection.CollectionName,
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
            try
            {
                using var traceSpan = TraceSpan(parent: parentSpan);

                Logger.LogInformation("[{attemptId}] Entering query mode", AttemptId);

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
                    txdata: txdata.ToDictionary(),
                    parentSpan: traceSpan.Item
                ).CAF();

                // set query mode here
                _opWrapper.SetQueryMode();

                // NOTE: the txid returned is the AttemptId, not the TransactionId.
                await foreach (var result in results.ConfigureAwait(false))
                {
                    if (result.txid != AttemptId)
                    {
                        Logger.LogWarning("BEGIN WORK returned '{txid}', expected '{AttemptId}'", result.txid,
                            AttemptId);
                    }
                    else
                    {
                        Logger.LogDebug(result.ToString());
                    }
                }
            }
            finally
            {
                _opWrapper.ResetShouldBlockTaskStarting();
            }
        }

        private QueryOptions InitializeBeginWorkQueryOptions(QueryOptions queryOptions)
        {
            queryOptions
                .ScanConsistency(_config.ScanConsistency ?? QueryScanConsistency.RequestPlus)
                .Raw("txtimeout", $"{_overallContext.RemainingUntilExpiration.TotalMilliseconds}ms");

            if (_config.MetadataCollection != null)
            {
                var mc = _config.MetadataCollection;
                queryOptions.Raw("atrcollection", $"`{mc.BucketName}`.`{mc.ScopeName}`.`{mc.CollectionName}`");
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
                || AttemptState == AttemptStates.NOTHING_WRITTEN
                || AttemptState == AttemptStates.COMPLETED
                || AttemptState == AttemptStates.ROLLED_BACK)
            {
                // nothing to clean up
                Logger.LogInformation("Skipping addition of cleanup request in state {s}", AttemptState);
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
                State: AttemptState,
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
