using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue.RangeScan;
using Couchbase.KeyValue.ZoneAware;
using Couchbase.Management.Query;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    internal sealed class CouchbaseCollection : ICouchbaseCollection, IBinaryCollection, IInternalCollection
    {
        public const string DefaultCollectionName = "_default";

        /// <summary>
        /// The collection ID of the default collection. Also the correct ID for a bucket that does
        /// not support collections at all, when the connection has negotiated collections.
        /// </summary>
        internal const uint DefaultCollectionId = 0;
        private const string NoPreferredServerGroupMessage = "No preferred Server group was set in the ClusterOptions.";
        private readonly string? _preferredServerGroup;
        private readonly bool _rangeScanSupported;
        private readonly BucketBase _bucket;
        private readonly ILogger<GetResult> _getLogger;
        private readonly IOperationConfigurator _operationConfigurator;
        private readonly IRequestTracer _tracer;
        private readonly ITypeTranscoder _rawStringTranscoder = new RawStringTranscoder(InternalSerializationContext.DefaultTypeSerializer);
        private readonly IFallbackTypeSerializerProvider _fallbackTypeSerializerProvider;
        private Lazy<Task<uint?>>? GetCidLazyRetry = null;
        private Lazy<Task<uint?>>? GetCidLazyNoRetry = null;

        private readonly object _cidLock = new();

        internal CouchbaseCollection(BucketBase bucket, IOperationConfigurator operationConfigurator,
            ILogger<CouchbaseCollection> logger,
            ILogger<GetResult> getLogger, IRedactor redactor,
            string name, IScope scope, IRequestTracer tracer, IFallbackTypeSerializerProvider fallbackTypeSerializerProvider,
            IServiceProvider serviceProvider)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _operationConfigurator =
                operationConfigurator ?? throw new ArgumentNullException(nameof(operationConfigurator));
            _getLogger = getLogger ?? throw new ArgumentNullException(nameof(getLogger));
            _tracer = tracer;
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _fallbackTypeSerializerProvider = fallbackTypeSerializerProvider ?? throw new ArgumentNullException(nameof(fallbackTypeSerializerProvider));

            IsDefaultCollection = scope.IsDefaultScope && name == DefaultCollectionName;
            if (_bucket.CurrentConfig != null)
            {
                if (_bucket.CurrentConfig.BucketCapabilities.Contains(BucketCapabilities.RANGE_SCAN)) _rangeScanSupported = true;
            }
            _preferredServerGroup = _bucket.Context.ClusterOptions.PreferredServerGroup;
            _lazyQueryIndexManagerFactory = new LazyService<ICollectionQueryIndexManagerFactory>(serviceProvider);

            if (_bucket is CouchbaseBucket couchBucket)
            {
                SubdocAccessDeleted = couchBucket.CurrentConfig?.BucketCapabilities.Contains(BucketCapabilities
                    .SUBDOC_ACCESS_DELETED) == true;
            }
        }

        internal IRedactor Redactor { get; }

        /// <inheritdoc />
        public string ScopeName => Scope.Name;

        /// <inheritdoc />
        public uint? Cid { get; set; }

        public ILogger<CouchbaseCollection> Logger { get; }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public IScope Scope { get; }

        /// <inheritdoc />
        public IBinaryCollection Binary => this;

        /// <inheritdoc />
        public bool IsDefaultCollection { get; }

        /// <inheritdoc />
        public bool SubdocAccessDeleted { get; }

        #region KV Range Scan

        [InterfaceStability(Level.Volatile)]
        public async IAsyncEnumerable<IScanResult> ScanAsync(IScanType scanType, ScanOptions? options = null)
        {
            //fail-fast if the server doesn't support range scans
            if (!_rangeScanSupported)
            {
                throw new FeatureNotAvailableException(
                    "This Cluster version does not support the scan operation (Only supported with Couchbase Server 7.6 and later).");
            }

            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= ScanOptions.Default;

            //PartitionScan builds its own operations and reads Cid directly, so it cannot go
            //through PrepareAsync: resolve the collection ID before handing it this collection.
            await EnsureCollectionIdAsync().ConfigureAwait(false);

            var mutationTokens = options.ConsistencyTokens;

            var partitionCount = (short)_bucket.CurrentConfig!.VBucketServerMap.VBucketMap.Length;
            var partitionScans = new List<PartitionScan>(partitionCount);
            for (short partitionId = 0; partitionId < partitionCount; partitionId++)
            {
                var partitionScan = new PartitionScan(_operationConfigurator, _bucket, this, _getLogger, options, scanType,partitionId);

                if (mutationTokens != null && mutationTokens.ContainsKey(partitionId))
                {
                    partitionScan.MutationToken = mutationTokens[partitionId];
                }
                partitionScans.Add(partitionScan);

            }

            //randomize the scan tasks
            partitionScans.Shuffle();

            //hacky but only sample scans have a global limit
            var isSamplingScan = false;
            var limit = 0ul;
            if (scanType is SamplingScan samplingScan)
            {
                isSamplingScan = true;
                limit = samplingScan.Limit;
            }

            var emptyPartitions = 0;
            var count = 0ul;
            while (emptyPartitions < partitionCount && !options.TokenValue.IsCancellationRequested)
            {
                foreach (var partitionScan in partitionScans.Where(x => x.Status != ResponseStatus.RangeScanComplete))
                {
                    var result = await partitionScan.ScanAsync().ConfigureAwait(false);

                    if (partitionScan.Status == ResponseStatus.Success ||
                        partitionScan.Status == ResponseStatus.RangeScanComplete ||
                        partitionScan.Status == ResponseStatus.RangeScanMore)
                    {
                        foreach (var scanResult in result.Results.Values)
                        {
                            var overLimit = isSamplingScan && count >= limit;
                            if (overLimit || scanResult == null)
                            {
                                _getLogger.LogDebug("Closing any leftover scans.");
                                await CloseAll(partitionScans).ConfigureAwait(false);
                                yield break;
                            }
                            yield return scanResult;
                            count++;
                        }
                    }
                }
                emptyPartitions = partitionScans.Count(x =>
                    x.Status == ResponseStatus.RangeScanComplete ||
                    x.Status == ResponseStatus.KeyNotFound);
            }
        }

        private async Task CloseAll(List<PartitionScan> partitionScans)
        {
            var partitionsToClose = partitionScans.Where(x => x.CanBeCanceled).Select(x => x.CancelAsync()).ToArray();
            await Task.WhenAll(partitionsToClose).ConfigureAwait(false);
        }

        #endregion

        #region Get

        /// <inheritdoc />
        public async Task<IGetResult> GetAsync(string id, GetOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= GetOptions.Default;

            // TODO: Since we're actually using LookupIn for Get requests, which operation name should we use?
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Get, options.RequestSpanValue);

            var projectList = options.ProjectListValue;

            var specCount = projectList.Count;
            if (options.IncludeExpiryValue) specCount++;

            if (specCount == 0)
            {
                // We aren't including the expiry value and we have no projections so fetch the whole doc using a Get operation
                using var getOp = new Get<byte[]>
                {
                    Key = id,
                    Span = rootSpan,
                    PreferReturns = options.PreferReturn
                };
                using var ctp = await PrepareAsync(getOp, options).ConfigureAwait(false);
                var status = await _bucket.RetryAsync(getOp, ctp.TokenPair).ConfigureAwait(false);

                var result = new GetResult(getOp.ExtractBody(), getOp.Transcoder, _getLogger, _fallbackTypeSerializerProvider, status)
                {
                    Id = getOp.Key,
                    Cas = getOp.Cas,
                    OpCode = getOp.OpCode,
                    Flags = getOp.Flags,
                    Header = getOp.Header,
                    Opaque = getOp.Opaque
                };
                return result;
            }

            var specs = new List<LookupInSpec>();

            if (options.IncludeExpiryValue)
                specs.Add(new LookupInSpec
                {
                    OpCode = OpCode.SubGet,
                    Path = VirtualXttrs.DocExpiryTime,
                    PathFlags = SubdocPathFlags.Xattr
                });

            if (projectList.Count == 0 || specCount > 16)
                // No projections or we have exceeded the max #fields returnable by sub-doc so fetch the whole doc
                specs.Add(new LookupInSpec
                {
                    Path = "",
                    OpCode = OpCode.Get,
                    DocFlags = SubdocDocFlags.None
                });
            else
                //Add the projections for fetching
                foreach (var path in projectList)
                    specs.Add(new LookupInSpec
                    {
                        OpCode = OpCode.SubGet,
                        Path = path
                    });

            var lookupInOptions = !ReferenceEquals(options, GetOptions.Default)
                ? new LookupInOptions()
                    .Timeout(options.TimeoutValue)
                    .Transcoder(options.TranscoderValue).AsReadOnly()
                : LookupInOptions.Default.AsReadOnly();

            using var lookupOp = await ExecuteLookupIn(id,
                    specs, lookupInOptions, rootSpan)
                .ConfigureAwait(false);
            rootSpan.WithOperationId(lookupOp);
            return new GetResult(lookupOp.ExtractBody(), lookupOp.Transcoder, _getLogger, _fallbackTypeSerializerProvider, specs, projectList)
            {
                Id = lookupOp.Key,
                Cas = lookupOp.Cas,
                OpCode = lookupOp.OpCode,
                Flags = lookupOp.Flags,
                Header = lookupOp.Header,
                Opaque = lookupOp.Opaque
            };
        }

        #endregion

        #region Exists

        /// <inheritdoc />
        public async Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= ExistsOptions.Default;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetMetaExists, options.RequestSpanValue);
            using var getMetaOp = new GetMeta
            {
                Key = id,
                Span = rootSpan
            };
            using var ctp = await PrepareAsync(getMetaOp, options).ConfigureAwait(false);
            var status = await _bucket.RetryAsync(getMetaOp, ctp.TokenPair).ConfigureAwait(false);
            var result = getMetaOp.GetValue();

            return new ExistsResult
            {
                Cas = getMetaOp.Cas,
                Exists = !result.Deleted && status == ResponseStatus.Success
            };
        }

        #endregion

        #region Insert

        /// <inheritdoc />
        public async Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null)
        {
            if (content is null) throw new InvalidArgumentException($"Parameter {nameof(content)} cannot be null.");

            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= InsertOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.AddInsert, options.RequestSpanValue);
            using var insertOp = new Add<T>(_bucket.Name, id)
            {
                Content = content,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            using var ctp = await PrepareAsync(insertOp, options).ConfigureAwait(false);
            await _bucket.RetryAsync(insertOp, ctp.TokenPair).ConfigureAwait(false);
            return new MutationResult(insertOp.Cas, null, insertOp.MutationToken);
        }

        #endregion

        #region Replace

        /// <inheritdoc />
        public async Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null)
        {
            if (content is null) throw new InvalidArgumentException($"Parameter {nameof(content)} cannot be null.");

            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= ReplaceOptions.Default;

            //Reality check for preserveTtl server support
            if (!_bucket.Context.SupportsPreserveTtl && options.PreserveTtlValue)
            {
                throw new FeatureNotAvailableException(
                    "This version of Couchbase Server does not support preserving expiry when modifying documents.");
            }

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Replace, options.RequestSpanValue);
            using var replaceOp = new Replace<T>(_bucket.Name, id)
            {
                Content = content,
                Cas = options.CasValue,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                PreserveTtl = options.PreserveTtlValue
            };
            using var ctp = await PrepareAsync(replaceOp, options).ConfigureAwait(false);
            var status = await _bucket.RetryAsync(replaceOp, ctp.TokenPair).ConfigureAwait(false);
            return new MutationResult(replaceOp.Cas, null, replaceOp.MutationToken, status);
        }

        #endregion

        #region Remove

        /// <inheritdoc />
        public async Task RemoveAsync(string id, RemoveOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= RemoveOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.DeleteRemove, options.RequestSpanValue);
            using var removeOp = new Delete
            {
                Key = id,
                Cas = options.CasValue,
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            using var ctp = await PrepareAsync(removeOp, options).ConfigureAwait(false);
            var status = await _bucket.RetryAsync(removeOp, ctp.TokenPair).ConfigureAwait(false);
            options.Status = status;
        }

        #endregion

        #region Unlock

        /// <inheritdoc />
        [Obsolete("Use overload that does not have a Type parameter T.")]
        public async Task UnlockAsync<T>(string id, ulong cas, UnlockOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= UnlockOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Unlock, options.RequestSpanValue);
            using var unlockOp = new Unlock
            {
                Key = id,
                Cas = cas,
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            using var ctp = await PrepareAsync(unlockOp, options).ConfigureAwait(false);
            await _bucket.RetryAsync(unlockOp, ctp.TokenPair).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UnlockAsync(string id, ulong cas, UnlockOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= UnlockOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Unlock);
            using var unlockOp = new Unlock
            {
                Key = id,
                Cas = cas,
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            using var ctp = await PrepareAsync(unlockOp, options).ConfigureAwait(false);
            var status = await _bucket.RetryAsync(unlockOp, ctp.TokenPair).ConfigureAwait(false);
            options.Status = status;
        }

        #endregion

        #region Touch

        /// <inheritdoc />
        public async Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null)
        {
            _ = await TouchWithCasAsync(id, expiry, options).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<IMutationResult?> TouchWithCasAsync(string id, TimeSpan expiry, TouchOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= TouchOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Touch, options.RequestSpanValue);
            using var touchOp = new Touch
            {
                Key = id,
                Expires = expiry.ToTtl(),
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan,
                PreferReturns = options.PreferReturn,
            };
            using var ctp = await PrepareAsync(touchOp, options).ConfigureAwait(false);
            var status = await _bucket.RetryAsync(touchOp, ctp.TokenPair).ConfigureAwait(false);
            options.Status = status;
            return status == ResponseStatus.Success
                ? new MutationResult(touchOp.Cas, null, touchOp.MutationToken, status)
                : null;
        }

        #endregion

        #region GetAndTouch

        /// <inheritdoc />
        public async Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= GetAndTouchOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndTouch, options.RequestSpanValue);
            using var getAndTouchOp = new GetT<byte[]>(_bucket.Name, id)
            {
                Expires = expiry.ToTtl(),
                Span = rootSpan
            };
            using var ctp = await PrepareAsync(getAndTouchOp, options).ConfigureAwait(false);
            await _bucket.RetryAsync(getAndTouchOp, ctp.TokenPair).ConfigureAwait(false);

            return new  GetResult(getAndTouchOp.ExtractBody(), getAndTouchOp.Transcoder, _getLogger, _fallbackTypeSerializerProvider)
            {
                Id = getAndTouchOp.Key,
                Cas = getAndTouchOp.Cas,
                Flags = getAndTouchOp.Flags,
                Header = getAndTouchOp.Header,
                OpCode = getAndTouchOp.OpCode
            };
        }

        #endregion

        #region GetAndLock

        /// <inheritdoc />
        public async Task<IGetResult> GetAndLockAsync(string id, TimeSpan lockTime, GetAndLockOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= GetAndLockOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndLock, options.RequestSpanValue);
            using var getAndLockOp = new GetL<byte[]>
            {
                Key = id,
                Expiry = lockTime.ToTtl(),
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            using var ctp = await PrepareAsync(getAndLockOp, options).ConfigureAwait(false);
            var status = await _bucket.RetryAsync(getAndLockOp, ctp.TokenPair).ConfigureAwait(false);
            return new GetResult(getAndLockOp.ExtractBody(), getAndLockOp.Transcoder, _getLogger, _fallbackTypeSerializerProvider, status)
            {
                Id = getAndLockOp.Key,
                Cas = getAndLockOp.Cas,
                Flags = getAndLockOp.Flags,
                Header = getAndLockOp.Header,
                OpCode = getAndLockOp.OpCode
            };
        }

        #endregion

        #region Upsert

        /// <inheritdoc />
        public async Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null)
        {
            if (content is null) throw new InvalidArgumentException($"Parameter {nameof(content)} cannot be null.");

            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= UpsertOptions.Default;

            //Reality check for preserveTtl server support
            if (!_bucket.Context.SupportsPreserveTtl && options.PreserveTtlValue)
            {
                throw new FeatureNotAvailableException(
                    "This version of Couchbase Server does not support preserving expiry when modifying documents.");
            }

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.SetUpsert, options.RequestSpanValue);
            using var upsertOp = new Set<T>(_bucket.Name, id)
            {
                Content = content,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                PreserveTtl = options.PreserveTtlValue
            };

            using var ctp = await PrepareAsync(upsertOp, options).ConfigureAwait(false);
            await _bucket.RetryAsync(upsertOp, ctp.TokenPair).ConfigureAwait(false);
            return new MutationResult(upsertOp.Cas, null, upsertOp.MutationToken);
        }

        #endregion

        #region LookupIn

        /// <inheritdoc />
        public async Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs,
            LookupInOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();
            if (specs.Count() > 16) throw new InvalidArgumentException("Too many specs in Lookup operation (Limited to 16)");
            var opts = options?.AsReadOnly() ?? LookupInOptions.DefaultReadOnly;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.LookupIn, opts.RequestSpan);
            using var lookup = await ExecuteLookupIn(id, specs, opts, rootSpan).ConfigureAwait(false);
            var responseStatus = lookup.Header.Status;
            var isDeleted = responseStatus == ResponseStatus.SubDocSuccessDeletedDocument ||
                            responseStatus == ResponseStatus.SubdocMultiPathFailureDeleted;
            return new LookupInResult(lookup, isDeleted); //Transcoder is set by OperationConfigurator
        }



        public async Task<ILookupInReplicaResult> LookupInAnyReplicaAsync(string id,
            IEnumerable<LookupInSpec> specs,
            LookupInAnyReplicaOptions? options = null)
        {
            _bucket.AssertCap(BucketCapabilities.SUBDOC_REPLICA_READ);
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();
            var opts = options?.AsReadOnly() ?? LookupInAnyReplicaOptions.DefaultReadOnly;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.LookupInAnyReplica, opts.RequestSpan);
            var vBucket = VBucketForReplicas(id);
            var enumeratedSpecs = specs.ToList();
            var indexesInGroup = ResolveZoneAwareGroupIndexes(id, vBucket, opts.ReadPreferenceValue);

            var tasks = indexesInGroup is null
                ? LookupInTasksForAllReplicas(vBucket, id, enumeratedSpecs, rootSpan, opts)
                : LookupInTasksForServerGroup(vBucket, indexesInGroup, id, enumeratedSpecs, rootSpan, opts);

            var completed = TaskHelpers.WhenAnySuccessful(tasks, opts.Token);
            try
            {
                await completed.ConfigureAwait(false);
            }
            catch (AggregateException e)
            {
                throw new DocumentUnretrievableException(e);
            }
            using var lookup = completed.Result;
            var responseStatus = lookup.Header.Status;
            var isDeleted = responseStatus is ResponseStatus.SubDocSuccessDeletedDocument or ResponseStatus.SubdocMultiPathFailureDeleted;
            return new LookupInResult(lookup, isDeleted, isReplica: lookup.ReplicaIdx != null);
        }

        public IAsyncEnumerable<ILookupInReplicaResult> LookupInAllReplicasAsync(string id,
            IEnumerable<LookupInSpec> specs,
            LookupInAllReplicasOptions? options = null)
        {
            // The stream below only runs once enumerated, so anything that must fail at the call site
            // like LookupInAnyReplica does is resolved here. The key is mapped once and handed over.
            _bucket.AssertCap(BucketCapabilities.SUBDOC_REPLICA_READ);

            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            var opts = options?.AsReadOnly() ?? LookupInAllReplicasOptions.DefaultReadOnly;
            var vBucket = VBucketForReplicas(id);
            var indexesInGroup = ResolveZoneAwareGroupIndexes(id, vBucket, opts.ReadPreferenceValue);

            return LookupInAllReplicasStreamAsync(id, specs, opts, vBucket, indexesInGroup);
        }

        private async IAsyncEnumerable<ILookupInReplicaResult> LookupInAllReplicasStreamAsync(string id,
            IEnumerable<LookupInSpec> specs,
            LookupInOptions.ReadOnly opts,
            VBucket vBucket,
            int[]? indexesInGroup)
        {
            // A top-level failure of the lookup (here, too many specs) must produce an empty stream
            // rather than throwing - unlike LookupIn/LookupInAnyReplica.
            if (specs.Count() > 16) yield break;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.LookupInAllReplicas, opts.RequestSpan);
            var enumeratedSpecs = specs.ToList();

            var tasks = indexesInGroup is null
                ? LookupInTasksForAllReplicas(vBucket, id, enumeratedSpecs, rootSpan, opts)
                : LookupInTasksForServerGroup(vBucket, indexesInGroup, id, enumeratedSpecs, rootSpan, opts);

            foreach (var lookupTask in tasks)
            {
                MultiLookup<byte[]> lookup;
                try
                {
                    lookup = await lookupTask.ConfigureAwait(false);
                }
                catch (CouchbaseException)
                {
                    // A replica (or the active) failed this lookup at the top level - e.g. an invalid
                    // or too-long path. Per the lookupInAllReplicas contract such failures are omitted
                    // from the stream (yielding an empty stream when they all fail) rather than
                    // faulting it.
                    continue;
                }

                var responseStatus = lookup.Header.Status;
                var isDeleted = responseStatus == ResponseStatus.SubDocSuccessDeletedDocument ||
                                responseStatus == ResponseStatus.SubdocMultiPathFailureDeleted;
                yield return new LookupInResult(lookup, isDeleted, isReplica: lookup.ReplicaIdx != null);
            }
        }

        private List<Task<MultiLookup<byte[]>>> LookupInTasksForAllReplicas(VBucket vBucket, string id,
            List<LookupInSpec> specs, IRequestSpan span, LookupInOptions.ReadOnly options)
        {
            var tasks = new List<Task<MultiLookup<byte[]>>> { ExecuteLookupIn(id, specs, options, span) };

            tasks.AddRange(GetReplicaIndexes(vBucket).Select(index =>
                ExecuteLookupIn(id, specs, options with { ReplicaIndex = index }, span)));

            return tasks;
        }

        private List<Task<MultiLookup<byte[]>>> LookupInTasksForServerGroup(VBucket vBucket, int[] indexesInGroup,
            string id, List<LookupInSpec> specs, IRequestSpan span, LookupInOptions.ReadOnly options)
        {
            var tasks = GetReplicaIndexes(vBucket)
                .Where(index => indexesInGroup.Contains(index))
                .Select(index => ExecuteLookupIn(id, specs, options with { ReplicaIndex = index }, span))
                .ToList();

            if (indexesInGroup.Contains(vBucket.Primary))
            {
                tasks.Add(ExecuteLookupIn(id, specs, options, span));
            }

            return tasks;
        }

        private async Task<MultiLookup<byte[]>> ExecuteLookupIn(string id, IEnumerable<LookupInSpec> specs,
            LookupInOptions.ReadOnly options, IRequestSpan span)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //add the virtual xattr attribute to get the doc expiration time
            if (options.Expiry)
            {
                specs = specs.Concat(new [] {
                    new LookupInSpec
                    {
                        Path = VirtualXttrs.DocExpiryTime,
                        OpCode = OpCode.SubGet,
                        PathFlags = SubdocPathFlags.Xattr,
                        DocFlags = SubdocDocFlags.None
                    }
                });
            }
            // if we are a replica read _and_ wanting access deleted, we have to be sure to
            // check that AccessDeleted is "fully" supported...
            var docFlags = options.AccessDeleted ? SubdocDocFlags.AccessDeleted : SubdocDocFlags.None;
            docFlags |= options.ReplicaIndex.HasValue ? SubdocDocFlags.ReplicaRead : SubdocDocFlags.None;
            if (options is { AccessDeleted: true, ReplicaIndex: not null } && !SubdocAccessDeleted)
            {
                docFlags &= ~SubdocDocFlags.AccessDeleted;
            }

            // if the server doesn't support binary xattr, strip the flag from the specs
            if (!_bucket.Context.SupportsBinaryXattr)
            {
                foreach (var spec in specs)
                {
                    spec.PathFlags &= ~SubdocPathFlags.BinaryValue;
                }
            }

            var lookup = new MultiLookup<byte[]>(id, specs, options.ReplicaIndex)
            {
                DocFlags = docFlags,
                Span = span,
                PreferReturns = options.PreferReturn,
            };
            try
            {
                using var ctp = await PrepareAsync(lookup, options).ConfigureAwait(false);
                var status = await _bucket.RetryAsync(lookup, ctp.TokenPair).ConfigureAwait(false);
                return lookup;
            }
            catch
            {
                // Make sure we cleanup the operation in the error case where it isn't returned
                lookup.Dispose();
                throw;
            }
        }

        #endregion

        #region MutateIn

        /// <inheritdoc />
        public async Task<IMutateInResult> MutateInAsync(string id, IEnumerable<MutateInSpec> specs,
            MutateInOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= MutateInOptions.Default;

            //Reality check for preserveTtl server support
            if (!_bucket.Context.SupportsPreserveTtl && options.PreserveTtlValue)
            {
                throw new FeatureNotAvailableException(
                    "This version of Couchbase Server does not support preserving expiry when modifying documents.");
            }

            //resolve StoreSemantics to SubdocDocFlags
            var docFlags = SubdocDocFlags.None;
            switch (options.StoreSemanticsValue)
            {
                case StoreSemantics.Replace:
                    break;
                case StoreSemantics.Upsert:
                    docFlags |= SubdocDocFlags.UpsertDocument;
                    break;
                case StoreSemantics.Insert:
                    docFlags |= SubdocDocFlags.InsertDocument;
                    break;
                case StoreSemantics.AccessDeleted:
                    docFlags |= SubdocDocFlags.AccessDeleted;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (options.CreateAsDeletedValue)
            {
                if (!_bucket.CurrentConfig?.BucketCapabilities.Contains(BucketCapabilities.CREATE_AS_DELETED) == true)
                    throw new FeatureNotAvailableException(nameof(BucketCapabilities.CREATE_AS_DELETED));

                docFlags |= SubdocDocFlags.CreateAsDeleted;
            }
            if (options.ReviveDocumentValue)
            {
                // We insist on AccessDeleted being set whenever we set ReviveDocument.
                if (!_bucket.CurrentConfig?.BucketCapabilities.Contains(BucketCapabilities.SUBDOC_REVIVE_DOCUMENT) == true)
                    throw new FeatureNotAvailableException(nameof(BucketCapabilities.SUBDOC_REVIVE_DOCUMENT));
                docFlags |= SubdocDocFlags.ReviveDocument | SubdocDocFlags.AccessDeleted;
            }

            if (options.AccessDeletedValue ) docFlags |= SubdocDocFlags.AccessDeleted;

            // if the server doesn't support binary xattrs, strip the flag from the specs
            if (!_bucket.Context.SupportsBinaryXattr)
            {
                foreach(var spec in specs)
                {
                    spec.PathFlags &= ~SubdocPathFlags.BinaryValue;
                }
            }

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.MutateIn, options.RequestSpanValue);
            using var mutation = new MultiMutation<byte[]>(id, specs)
            {
                Cas = options.CasValue,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DocFlags = docFlags,
                OptionalFlags = _bucket.Context.SupportsBinaryXattr ? options.FlagsValue : null,
                Span = rootSpan,
                PreserveTtl = options.PreserveTtlValue
            };
            using var ctp = await PrepareAsync(mutation, options).ConfigureAwait(false);
            await _bucket.RetryAsync(mutation, ctp.TokenPair).ConfigureAwait(false);

#pragma warning disable 618 // MutateInResult is marked obsolete until it is made internal
            return new MutateInResult(mutation);
#pragma warning restore 618
        }

        private TimeSpan GetTimeout(TimeSpan? optionsTimeout, IOperation op)
        {
            if (optionsTimeout == null || optionsTimeout.Value == TimeSpan.Zero)
            {
                if (op.HasDurability)
                {
                    op.Timeout = _bucket.Context.ClusterOptions.KvDurabilityTimeout;
                    return op.Timeout;
                }

                optionsTimeout = _bucket.Context.ClusterOptions.KvTimeout;
            }

            return op.Timeout = optionsTimeout.Value;
        }

        #endregion

        #region Append

        /// <inheritdoc />
        public async Task<IMutationResult> AppendAsync(string id, byte[] value, AppendOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= AppendOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Append, options.RequestSpanValue);
            using var op = new Append<byte[]>(_bucket.Name, id)
            {
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Cas = options.CasValue
            };
            using var ctp = await PrepareAsync(op, options).ConfigureAwait(false);
            await _bucket.RetryAsync(op, ctp.TokenPair).ConfigureAwait(false);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Prepend

        /// <inheritdoc />
        public async Task<IMutationResult> PrependAsync(string id, byte[] value, PrependOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= PrependOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Prepend, options.RequestSpanValue);
            using var op = new Prepend<byte[]>(_bucket.Name, id)
            {
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Cas = options.CasValue
            };
            using var ctp = await PrepareAsync(op, options).ConfigureAwait(false);
            await _bucket.RetryAsync(op, ctp.TokenPair).ConfigureAwait(false);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Increment

        /// <inheritdoc />
        public async Task<ICounterResult> IncrementAsync(string id, IncrementOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= IncrementOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Increment, options.RequestSpanValue);
            using var op = new Increment(_bucket.Name, id)
            {
                Delta = options.DeltaValue,
                Initial = options.InitialValue,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Expires = options.ExpiryValue.ToTtl()
            };
            using var ctp = await PrepareAsync(op, options).ConfigureAwait(false);
            await _bucket.RetryAsync(op, ctp.TokenPair).ConfigureAwait(false);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Decrement

        /// <inheritdoc />
        public async Task<ICounterResult> DecrementAsync(string id, DecrementOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= DecrementOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Decrement, options.RequestSpanValue);
            using var op = new Decrement(_bucket.Name, id)
            {
                Delta = options.DeltaValue,
                Initial = options.InitialValue,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Expires = options.ExpiryValue.ToTtl()
            };
            using var ctp = await PrepareAsync(op, options).ConfigureAwait(false);
            await _bucket.RetryAsync(op, ctp.TokenPair).ConfigureAwait(false);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion

        #region GetAnyReplica / GetAllReplicas

        /// <inheritdoc />
        public async Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= GetAnyReplicaOptions.Default;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAnyReplica, options.RequestSpanValue);
            var vBucket = VBucketForReplicas(id);
            var indexesInGroup = ResolveZoneAwareGroupIndexes(id, vBucket, options.ReadPreferenceValue);

            var tasks = indexesInGroup is null
                ? GetTasksForAllReplicas(vBucket, id, rootSpan, options.TokenValue, options)
                : GetTasksForServerGroup(vBucket, indexesInGroup, id, rootSpan, options.TokenValue, options);

            var firstCompleted = TaskHelpers.WhenAnySuccessful(tasks, options.TokenValue);
            try
            {
                await firstCompleted.ConfigureAwait(false);
            }
            catch (AggregateException e)
            {
                throw new DocumentUnretrievableException(e);
            }

            return firstCompleted.Result;
        }

        private string ZoneAwareUnretrievableMessage(string id) =>
            $"Either neither the primary or replicas for Document: {id}" +
            $" live in the selected Server Group: {_preferredServerGroup}," +
            $" or no node/group matches could be made from the config.";

        /// <summary>
        /// The preferred server group node indexes to read the document from, or null when every replica
        /// should be read: either no preference was asked for, or the group cannot serve the document and
        /// the fallback was asked for.
        /// </summary>
        /// <exception cref="DocumentUnretrievableException">
        /// The group cannot serve the document and no fallback was asked for, or no group was selected in
        /// the <see cref="ClusterOptions"/> at all, which no read preference can work around.
        /// </exception>
        private int[]? ResolveZoneAwareGroupIndexes(string id, VBucket vBucket, InternalReadPreference readPreference)
        {
            if (readPreference == InternalReadPreference.NoPreference)
            {
                return null;
            }

            if (_preferredServerGroup is null)
            {
                throw new DocumentUnretrievableException(NoPreferredServerGroupMessage);
            }

            if (GetPreferredServerGroupIndexes() is { } indexesInGroup && GroupHoldsDocument(vBucket, indexesInGroup))
            {
                return indexesInGroup;
            }

            if (readPreference != InternalReadPreference.SelectedServerGroupWithFallback)
            {
                throw new DocumentUnretrievableException(ZoneAwareUnretrievableMessage(id));
            }

            Logger.LogDebug("Falling back to all replicas with no server group preference for {Id}", Redactor.UserData(id));
            return null;
        }

        /// <summary>
        /// The node indexes of the preferred server group, or null when the group is unset, when no
        /// node/group pairs could be made from the config, or when the group holds no nodes.
        /// </summary>
        /// <remarks>
        /// <see cref="BucketConfig.ServerGroupNodeIndexes"/> is rebuilt on every access, so callers
        /// should resolve this once per operation.
        /// </remarks>
        private int[]? GetPreferredServerGroupIndexes()
        {
            if (_preferredServerGroup is null
                || _bucket.CurrentConfig?.ServerGroupNodeIndexes is not { } groupNodeIndexes
                || !groupNodeIndexes.TryGetValue(_preferredServerGroup, out var indexesInGroup)
                || indexesInGroup is not { Length: > 0 })
            {
                return null;
            }

            return indexesInGroup;
        }

        /// <summary>
        /// Whether the given group node indexes hold the primary or any replica of the document.
        /// </summary>
        private static bool GroupHoldsDocument(VBucket vBucket, int[] indexesInGroup) =>
            indexesInGroup.Contains(vBucket.Primary)
            || vBucket.Replicas.Any(index => index > -1 && indexesInGroup.Contains(index));

        private VBucket VBucketForReplicas(string id, [CallerMemberName]string caller = "AnyReplica")
        {
            var vBucket = (VBucket)_bucket.KeyMapper!.MapKey(id);

            if (!vBucket.HasReplicas)
                Logger.LogWarning("Call to {Caller} for key [{Id}] but none are configured. Only the active document will be retrieved", caller, id);
            return vBucket;
        }

        /// <inheritdoc />
        public IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id,
            GetAllReplicasOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= GetAllReplicasOptions.Default;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAllReplicas, options.RequestSpanValue);
            var vBucket = VBucketForReplicas(id);
            var indexesInGroup = ResolveZoneAwareGroupIndexes(id, vBucket, options.ReadPreferenceValue);

            return indexesInGroup is null
                ? GetTasksForAllReplicas(vBucket, id, rootSpan, options.TokenValue, options)
                : GetTasksForServerGroup(vBucket, indexesInGroup, id, rootSpan, options.TokenValue, options);
        }

        private static List<short> GetReplicaIndexes(VBucket vBucket)
        {
            var replicas = vBucket.Replicas.Where(index => index > -1).ToList();
            return replicas;
        }

        private List<Task<IGetReplicaResult>> GetTasksForAllReplicas(VBucket vBucket, string id, IRequestSpan span,
            CancellationToken token, ITranscoderOverrideOptions options)
        {
            var tasks = new List<Task<IGetReplicaResult>> { GetPrimary(id, span, token, options) };

            tasks.AddRange(GetReplicaIndexes(vBucket).Select(index => GetReplica(id, index, span, token, options)));

            return tasks;
        }

        private List<Task<IGetReplicaResult>> GetTasksForServerGroup(VBucket vBucket, int[] indexesInGroup, string id,
            IRequestSpan span, CancellationToken token, ITranscoderOverrideOptions options)
        {
            var tasks = GetReplicaIndexes(vBucket)
                .Where(index => indexesInGroup.Contains(index))
                .Select(index => GetReplica(id, index, span, token, options))
                .ToList();

            if (indexesInGroup.Contains(vBucket.Primary))
            {
                tasks.Add(GetPrimary(id, span, token, options));
            }

            return tasks;
        }

        private async Task<IGetReplicaResult> GetPrimary(string id, IRequestSpan span,
            CancellationToken cancellationToken, ITranscoderOverrideOptions options)
        {
            using var childSpan = _tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Kv.Get, span);
            using var getOp = new Get<object>
            {
                Key = id,
                Span = childSpan
            };
            using var ctp =
                await PrepareAsync(getOp, (ITimeoutOptions) options).ConfigureAwait(false);
            await _bucket.RetryAsync(getOp, ctp.TokenPair).ConfigureAwait(false);
            return new GetReplicaResult(getOp.ExtractBody(), getOp.Transcoder, _getLogger, _fallbackTypeSerializerProvider)
            {
                Id = getOp.Key,
                Cas = getOp.Cas,
                OpCode = getOp.OpCode,
                Flags = getOp.Flags,
                Header = getOp.Header,
                IsActive = true
            };
        }

        private async Task<IGetReplicaResult> GetReplica(string id, short index, IRequestSpan span,
            CancellationToken cancellationToken, ITranscoderOverrideOptions options)
        {
            using var childSpan = _tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Kv.ReplicaRead, span);
            using var getOp = new ReplicaRead<object>(id, index)
            {
                Key = id,
                Span = childSpan
            };
            using var ctp =
                await PrepareAsync(getOp, (ITimeoutOptions) options).ConfigureAwait(false);
            await _bucket.RetryAsync(getOp, ctp.TokenPair).ConfigureAwait(false);
            return new GetReplicaResult(getOp.ExtractBody(), getOp.Transcoder, _getLogger, _fallbackTypeSerializerProvider)
            {
                Id = getOp.Key,
                Cas = getOp.Cas,
                OpCode = getOp.OpCode,
                Flags = getOp.Flags,
                Header = getOp.Header,
                IsActive = false
            };
        }

        #endregion

        #region Operation Preparation

        /// <summary>
        /// Prepares a key/value operation for dispatch: resolves the collection ID if this bucket
        /// supports collections, stamps the operation with this collection's identity, applies the
        /// configured services, and rents the retry/timeout token source needed to send it.
        /// </summary>
        /// <param name="op">The operation to prepare.</param>
        /// <param name="options">Options for the operation.</param>
        /// <returns>
        /// The rented token source, which the caller owns and must dispose. The caller needs its
        /// <c>TokenPair</c> in order to dispatch, which is what stops this method from being
        /// skipped when a new operation is added: see NCBC-4285, where <c>GetPrimary</c> was a
        /// near-identical copy of <c>GetReplica</c> that never resolved its collection ID.
        /// </returns>
        private async Task<CancellationTokenPairSourceWrapper> PrepareAsync(OperationBase op,
            ITimeoutOptions options)
        {
            await EnsureCollectionIdAsync().ConfigureAwait(false);

            op.BucketName = _bucket.Name;
            op.SName = ScopeName;
            op.CName = Name;
            op.Cid = Cid;

            _operationConfigurator.Configure(op, options);

            return CreateRetryTimeoutCancellationTokenSource(options, op);
        }

        /// <summary>
        /// Resolves the collection ID, if this bucket supports collections and it is not already
        /// cached.
        /// </summary>
        /// <remarks>
        /// Prefer <see cref="PrepareAsync"/>, which does this as well and cannot be skipped. This is
        /// separate only for the range scan path: <see cref="PartitionScan"/> builds its own
        /// operations and reads <see cref="Cid"/> directly, so the ID has to be resolved before it
        /// is handed this collection.
        /// </remarks>
        private async ValueTask EnsureCollectionIdAsync()
        {
            if (!_bucket.SupportsCollections)
            {
                if (!IsDefaultCollection)
                {
                    //Asking for a named collection on a cluster that has none cannot be satisfied.
                    //Say so at the boundary rather than sending an operation whose collection the
                    //server has no way to resolve, which used to surface as CollectionNotFound after
                    //the full KvTimeout. Matches the JVM's
                    //BaseKeyValueRequest.encodedExternalKeyWithCollection.
                    throw new FeatureNotAvailableException(
                        "Collections are not supported by this cluster or bucket, so the collection " +
                        $"'{ScopeName}.{Name}' cannot be used.");
                }

                //Only the default collection exists here - a memcached bucket, or a pre-7.0 server.
                //The connection may still have negotiated collections in HELO, in which case the
                //server reads a leb128 collection ID from the start of every key, and the correct
                //value is 0. Leaving it null is why every KV operation against a memcached bucket
                //failed with CollectionNotFound after burning the full KvTimeout.
                Cid ??= DefaultCollectionId;
                return;
            }

            if (IsDefaultCollection)
            {
                //The default scope and collection are defined to be collection ID 0, so there is
                //nothing to look up. This saves a GET_CID round trip on the first operation against
                //every default collection, which is most of them.
                Cid ??= DefaultCollectionId;
                return;
            }

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }
        }


        #endregion

        #region GET_CID

        /// <summary>
        /// Servers 7.0 and above support collections and require the CID to be fetched.
        /// Earlier versions of the server may support collections in dev-preview mode so
        /// we check to see if its been enabled via the results of the HELLO command.
        /// </summary>
        /// <returns>true if the server supports collections and the CID is null.</returns>
        private bool RequiresCid()
        {
            return !Cid.HasValue && _bucket.SupportsCollections;
        }

        public async ValueTask PopulateCidAsync(bool retryIfFailure = true, bool forceUpdate = false)
        {
            // Short-circuit if we have the CID already
            if (!forceUpdate && Cid.HasValue)
            {
                return;
            }

            // old servers do not support collections so we exit
            if (!_bucket.SupportsCollections)
            {
                return;
            }

            if (forceUpdate)
            {
                Cid = await GetCidWithFallbackAsync(retryIfFailure).ConfigureAwait(false);
                lock (_cidLock)
                {
                    GetCidLazyRetry = null;
                    GetCidLazyNoRetry = null;
                }

                return;
            }
            else
            {
                lock (_cidLock)
                {
                    GetCidLazyRetry ??= new Lazy<Task<uint?>>(
                        () => GetCidWithFallbackAsync(retryIfFailure: true),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                    GetCidLazyNoRetry ??= new Lazy<Task<uint?>>(
                        () => GetCidWithFallbackAsync(retryIfFailure: false),
                        LazyThreadSafetyMode.ExecutionAndPublication);
                }

                try
                {
                    Cid = retryIfFailure
                        ? await GetCidLazyRetry.Value.ConfigureAwait(false)
                        : await GetCidLazyNoRetry.Value.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    lock (_cidLock)
                    {
                        GetCidLazyRetry = null;
                        GetCidLazyNoRetry = null;
                    }

                    throw;
                }
            }

            Logger.LogDebug("Completed fetching CID for {scope}.{collection}", ScopeName, Name);
        }

        /// <summary>
        /// Sends the scope/collection in the key or the operation body as content based on the flag.
        /// </summary>
        /// <param name="fullyQualifiedName">The fully qualified scope.collection name.</param>
        /// <param name="sendAsBody">true to send as the body; false in the key for dev-preview (pre-7.0 servers). </param>
        /// <param name="retryIfFailure">true to retry the CID operation if it fails.</param>
        /// <returns></returns>
        private async Task<uint?> GetCidAsync(string fullyQualifiedName, bool sendAsBody, bool retryIfFailure)
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Internal.GetCid);
            using var getCid = new GetCid
            {
                Opaque = SequenceGenerator.GetNext(),
                Span = rootSpan,
                SName = ScopeName,
                CName = Name
            };

            if (sendAsBody)
            {
                getCid.Content = fullyQualifiedName;
            }
            else
            {
                getCid.Key = fullyQualifiedName;
            }

            var options = new GetOptions();
            _operationConfigurator.Configure(getCid, options.Transcoder(_rawStringTranscoder));
            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, getCid);
            var status = retryIfFailure
                ? await _bucket.RetryAsync(getCid, ctp.TokenPair).ConfigureAwait(false)
                : await _bucket.SendAsync(getCid, ctp.TokenPair).ConfigureAwait(false);

            var resultWithValue = getCid.GetValueAsUint();

            if (status == ResponseStatus.Success && !resultWithValue.HasValue)
            {
                //GetValueAsUint returns null for a Success response whose body is empty or cannot be
                //parsed. That null used to be assigned to Cid and carried to the wire; it now fails
                //at framing time, but with a message about the operation rather than about the
                //lookup, so name what actually happened while we still know it. The deliberate null
                //from GetCidWithFallbackAsync's UnsupportedException path is untouched: that one
                //means the server has no collections, which is a different answer.
                ThrowHelper.ThrowCollectionIdNotResolvedException(ScopeName, Name);
            }

            return resultWithValue;
        }

        private async Task<uint?> GetCidWithFallbackAsync(bool retryIfFailure)
        {
            var fullyQualifiedName = $"{ScopeName}.{Name}";
            try
            {
                return await GetCidAsync(fullyQualifiedName, true, retryIfFailure).ConfigureAwait(false);
            }
            catch (Core.Exceptions.TimeoutException)
            {
                throw;
            }
            catch (Exception e)
            {
                Logger.LogInformation(e, "Possible non-terminal error fetching CID. Cluster may be in Dev-Preview mode.");
                if (e is InvalidArgumentException)
                    try
                    {
                        //if this is encountered were on a older server pre-cheshire cat changes
                        return await GetCidAsync($"{ScopeName}.{Name}", false, retryIfFailure).ConfigureAwait(false);
                    }
                    catch (UnsupportedException)
                    {
                        //an older server without collections enabled
                        Logger.LogInformation("Collections are not supported on this server version.");
                        return null;
                    }
                else
                {
                    throw;
                }
            }
        }
        #endregion

        #region tracing



        private IRequestSpan RootSpan(string operation, IRequestSpan? parentSpan = null)
        {
            var span = _tracer.RequestSpan(operation, parentSpan);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name);
                span.SetAttribute(OuterRequestSpans.Attributes.BucketName, _bucket.Name);
                span.SetAttribute(OuterRequestSpans.Attributes.ScopeName, ScopeName);
                span.SetAttribute(OuterRequestSpans.Attributes.CollectionName, Name);
                span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            }

            return span;
        }
        #endregion

        #region Timeouts

        private CancellationTokenPairSourceWrapper CreateRetryTimeoutCancellationTokenSource(ITimeoutOptions options, IOperation op) =>
            CancellationTokenPairSourcePool.Shared.Rent(GetTimeout(options.Timeout, op), options.Token);

        #endregion

        #region Index Management

        private readonly LazyService<ICollectionQueryIndexManagerFactory> _lazyQueryIndexManagerFactory;

        // It isn't imperative that race conditions accessing this field the first time must
        // always return the same singleton. In the unlikely event two threads access it the
        // first time simultaneously one may receive a temporary extra instance but that's okay.
        private ICollectionQueryIndexManager? _queryIndexManager;

        public ICollectionQueryIndexManager QueryIndexes => _queryIndexManager ??= _lazyQueryIndexManagerFactory.GetValueOrThrow().Create(_bucket, this);

        #endregion
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
