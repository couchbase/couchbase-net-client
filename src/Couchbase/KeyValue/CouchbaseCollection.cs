using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
using Couchbase.Management.Query;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class CouchbaseCollection : ICouchbaseCollection, IBinaryCollection, IInternalCollection
    {
        public const string DefaultCollectionName = "_default";
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

            _lazyQueryIndexManagerFactory = new LazyService<ICollectionQueryIndexManagerFactory>(serviceProvider);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= ScanOptions.Default;

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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

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
                    Cid = Cid,
                    CName = Name,
                    SName = ScopeName,
                    Span = rootSpan,
                    PreferReturns = options.PreferReturn
                };
                _operationConfigurator.Configure(getOp, options);

                using var ctp = CreateRetryTimeoutCancellationTokenSource(options, getOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= ExistsOptions.Default;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetMetaExists, options.RequestSpanValue);
            using var getMetaOp = new GetMeta
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Span = rootSpan
            };
            _operationConfigurator.Configure(getMetaOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, getMetaOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= InsertOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.AddInsert, options.RequestSpanValue);
            using var insertOp = new Add<T>(_bucket.Name, id)
            {
                Content = content,
                Cid = Cid,
                SName = ScopeName,
                CName = Name,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(insertOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, insertOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

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
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                PreserveTtl = options.PreserveTtlValue
            };
            _operationConfigurator.Configure(replaceOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, replaceOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= RemoveOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.DeleteRemove, options.RequestSpanValue);
            using var removeOp = new Delete
            {
                Key = id,
                Cas = options.CasValue,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            _operationConfigurator.Configure(removeOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, removeOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= UnlockOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Unlock, options.RequestSpanValue);
            using var unlockOp = new Unlock
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Cas = cas,
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            _operationConfigurator.Configure(unlockOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, unlockOp);
            await _bucket.RetryAsync(unlockOp, ctp.TokenPair).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UnlockAsync(string id, ulong cas, UnlockOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= UnlockOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Unlock);
            using var unlockOp = new Unlock
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Cas = cas,
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            _operationConfigurator.Configure(unlockOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, unlockOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= TouchOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Touch, options.RequestSpanValue);
            using var touchOp = new Touch
            {
                Key = id,
                Cid = Cid,
                SName = ScopeName,
                CName = Name,
                Expires = expiry.ToTtl(),
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan,
                PreferReturns = options.PreferReturn,
            };
            _operationConfigurator.Configure(touchOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, touchOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= GetAndTouchOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndTouch, options.RequestSpanValue);
            using var getAndTouchOp = new GetT<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Expires = expiry.ToTtl(),
                Span = rootSpan
            };
            _operationConfigurator.Configure(getAndTouchOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, getAndTouchOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= GetAndLockOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndLock, options.RequestSpanValue);
            using var getAndLockOp = new GetL<byte[]>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Expiry = lockTime.ToTtl(),
                Span = rootSpan,
                PreferReturns = options.PreferReturn
            };
            _operationConfigurator.Configure(getAndLockOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, getAndLockOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

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
                CName = Name,
                SName = ScopeName,
                Cid = Cid,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                PreserveTtl = options.PreserveTtlValue
            };

            _operationConfigurator.Configure(upsertOp, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, upsertOp);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

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
            var opts = options?.AsReadOnly() ?? LookupInOptions.DefaultReadOnly;

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.LookupInAnyReplica, opts.RequestSpan);
            var vBucket = VBucketForReplicas(id);
            var enumeratedSpecs = specs.ToList();
            var tasks = new List<Task<MultiLookup<byte[]>>>(vBucket.Replicas.Length + 1)
            {
                ExecuteLookupIn(id, enumeratedSpecs, opts, rootSpan)
            };
            if (vBucket.HasReplicas)
            {
                tasks.AddRange(vBucket.Replicas.Select(replica =>
                {
                    var replicaOpts = opts with { ReplicaIndex = replica };
                    return ExecuteLookupIn(id, enumeratedSpecs, replicaOpts, rootSpan);
                }));
            }

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

        public async IAsyncEnumerable<ILookupInReplicaResult> LookupInAllReplicasAsync(string id,
            IEnumerable<LookupInSpec> specs,
            LookupInAllReplicasOptions? options = null)
        {
            _bucket.AssertCap(BucketCapabilities.SUBDOC_REPLICA_READ);

            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();
            if (specs.Count() > 16) throw new InvalidArgumentException("Too many specs in Lookup operation (Limited to 16)");
            var opts = options?.AsReadOnly() ?? LookupInOptions.DefaultReadOnly;

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.LookupInAllReplicas, opts.RequestSpan);
            var vBucket = VBucketForReplicas(id);
            var enumeratedSpecs = specs.ToList();
            var tasks = new List<Task<MultiLookup<byte[]>>>(vBucket.Replicas.Length + 1)
            {
                ExecuteLookupIn(id, enumeratedSpecs, opts, rootSpan)
            };
            if (vBucket.HasReplicas)
            {
                tasks.AddRange(vBucket.Replicas.Select(replica =>
                {
                    var replicaOpts = opts with { ReplicaIndex = replica };
                    return ExecuteLookupIn(id, enumeratedSpecs, replicaOpts, rootSpan);
                }));
            }
            foreach (var lookupTask in tasks)
            {
                var lookup = await lookupTask.ConfigureAwait(false);
                var responseStatus = lookup.Header.Status;
                var isDeleted = responseStatus == ResponseStatus.SubDocSuccessDeletedDocument ||
                                responseStatus == ResponseStatus.SubdocMultiPathFailureDeleted;
                yield return new LookupInResult(lookup, isDeleted, isReplica: lookup.ReplicaIdx != null);
            }
        }

        private async Task<MultiLookup<byte[]>> ExecuteLookupIn(string id, IEnumerable<LookupInSpec> specs,
            LookupInOptions.ReadOnly options, IRequestSpan span)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //Get the collection ID
            await PopulateCidAsync().ConfigureAwait(false);

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

            var lookup = new MultiLookup<byte[]>(id, specs, options.ReplicaIndex)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                DocFlags = options.AccessDeleted ? SubdocDocFlags.AccessDeleted : (options.ReplicaIndex.HasValue ? SubdocDocFlags.ReplicaRead : SubdocDocFlags.None),
                Span = span,
                PreferReturns = options.PreferReturn,
            };
            try
            {
                _operationConfigurator.Configure(lookup, options);

                using var ctp = CreateRetryTimeoutCancellationTokenSource(options, lookup);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

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

            if (options.AccessDeletedValue) docFlags |= SubdocDocFlags.AccessDeleted;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.MutateIn, options.RequestSpanValue);
            using var mutation = new MultiMutation<byte[]>(id, specs)
            {
                BucketName = _bucket.Name,
                Cas = options.CasValue,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DocFlags = docFlags,
                Span = rootSpan,
                PreserveTtl = options.PreserveTtlValue
            };
            _operationConfigurator.Configure(mutation, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, mutation);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= AppendOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Append, options.RequestSpanValue);
            using var op = new Append<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Cas = options.CasValue
            };
            _operationConfigurator.Configure(op, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, op);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= PrependOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Prepend, options.RequestSpanValue);
            using var op = new Prepend<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Cas = options.CasValue
            };
            _operationConfigurator.Configure(op, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, op);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= IncrementOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Increment, options.RequestSpanValue);
            using var op = new Increment(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Delta = options.DeltaValue,
                Initial = options.InitialValue,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Expires = options.ExpiryValue.ToTtl()
            };
            _operationConfigurator.Configure(op, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, op);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= DecrementOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Decrement, options.RequestSpanValue);
            using var op = new Decrement(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Delta = options.DeltaValue,
                Initial = options.InitialValue,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Expires = options.ExpiryValue.ToTtl()
            };
            _operationConfigurator.Configure(op, options);

            using var ctp = CreateRetryTimeoutCancellationTokenSource(options, op);
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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= GetAnyReplicaOptions.Default;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAnyReplica, options.RequestSpanValue);
            var vBucket = VBucketForReplicas(id);

            // get primary
            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1)
            {
                GetPrimary(id, rootSpan, options.TokenValue, options)
            };

            // get replicas
            tasks.AddRange(
                vBucket.Replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, options)));

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

        private VBucket VBucketForReplicas(string id, [CallerMemberName]string caller = "AnyReplica")
        {
            var vBucket = (VBucket)_bucket.KeyMapper!.MapKey(id);

            if (!vBucket.HasReplicas)
                Logger.LogWarning(
                    $"Call to {caller} for key [{id}] but none are configured. Only the active document will be retrieved.");
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
            var vBucket = (VBucket) _bucket.KeyMapper!.MapKey(id);
            if (!vBucket.HasReplicas)
                Logger.LogWarning(
                    $"Call to GetAllReplicas for key [{id}] but none are configured. Only the active document will be retrieved.");

            //get a list of replica indexes
            var replicas = GetReplicaIndexes(vBucket);

            // get the primary
            var tasks = new List<Task<IGetReplicaResult>>(replicas.Count + 1)
            {
                GetPrimary(id, rootSpan, options.TokenValue, options)
            };

            // get the replicas
            tasks.AddRange(replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, options)));

            return tasks;
        }

        private static List<short> GetReplicaIndexes(VBucket vBucket)
        {
            var replicas = vBucket.Replicas.Where(index => index > -1).ToList();
            return replicas;
        }

        private async Task<IGetReplicaResult> GetPrimary(string id, IRequestSpan span,
            CancellationToken cancellationToken, ITranscoderOverrideOptions options)
        {
            using var childSpan = _tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Kv.Get, span);
            using var getOp = new Get<object>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Span = childSpan
            };
            _operationConfigurator.Configure(getOp, options);

            using var ctp =
                CreateRetryTimeoutCancellationTokenSource((ITimeoutOptions) options, getOp);
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
            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            using var childSpan = _tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Kv.ReplicaRead, span);
            using var getOp = new ReplicaRead<object>(id, index)
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Span = childSpan
            };
            _operationConfigurator.Configure(getOp, options);

            using var ctp =
                CreateRetryTimeoutCancellationTokenSource((ITimeoutOptions) options, getOp);
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
                        () => GetCidWithFallbackAsync(retryIfFailure: true),
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
            if (retryIfFailure)
            {
                await _bucket.RetryAsync(getCid, ctp.TokenPair).ConfigureAwait(false);
            }
            else
            {
                await _bucket.SendAsync(getCid, ctp.TokenPair).ConfigureAwait(false);
            }

            var resultWithValue = getCid.GetValueAsUint();
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

        private CancellationTokenPairSource CreateRetryTimeoutCancellationTokenSource(
            ITimeoutOptions options, IOperation op) =>
            CancellationTokenPairSource.FromTimeout(GetTimeout(options.Timeout, op), options.Token);

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
