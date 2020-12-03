using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    internal class CouchbaseCollection : ICouchbaseCollection, IBinaryCollection
    {
        public const string DefaultCollectionName = "_default";
        private readonly BucketBase _bucket;
        private readonly IOperationConfigurator _operationConfigurator;
        private readonly ILogger<GetResult> _getLogger;
        private readonly IRequestTracer _tracer;

        internal CouchbaseCollection(BucketBase bucket, IOperationConfigurator operationConfigurator, ILogger<CouchbaseCollection> logger,
            ILogger<GetResult> getLogger, IRedactor redactor,
            uint? cid, string name, IScope scope, IRequestTracer tracer)
        {
            Cid = cid;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _operationConfigurator = operationConfigurator ?? throw new ArgumentNullException(nameof(operationConfigurator));
            _getLogger = getLogger ?? throw new ArgumentNullException(nameof(getLogger));
            _tracer = tracer;
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        internal IRedactor Redactor { get; }

        public string ScopeName => Scope.Name;

        public uint? Cid { get; internal set; }

        public string Name { get; }

        /// <inheritdoc />
        public IScope Scope { get; }

        public IBinaryCollection Binary => this;

        public ILogger<CouchbaseCollection> Logger { get; }

        #region Get

        public async Task<IGetResult> GetAsync(string id, GetOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            // TODO: Since we're actually using LookupIn for Get requests, which operation name should we use?
            using var rootSpan = RootSpan(OperationNames.Get);
            options ??= new GetOptions();

            var projectList = options.ProjectListValue;

            var specCount = projectList.Count;
            if (options.IncludeExpiryValue)
            {
                specCount++;
            }

            if (specCount == 0)
            {
                // We aren't including the expiry value and we have no projections so fetch the whole doc using a Get operation

                var getOp = await ExecuteGet(id, options, rootSpan).ConfigureAwait(false);
                rootSpan.OperationId(getOp);

                return new GetResult(getOp.ExtractBody(), getOp.Transcoder, _getLogger)
                {
                    Id = getOp.Key,
                    Cas = getOp.Cas,
                    OpCode = getOp.OpCode,
                    Flags = getOp.Flags,
                    Header = getOp.Header,
                    Opaque = getOp.Opaque
                };
            }

            var specs = new List<LookupInSpec>();

            if (options.IncludeExpiryValue)
            {
                specs.Add(new LookupInSpec
                {
                    OpCode = OpCode.SubGet,
                    Path = VirtualXttrs.DocExpiryTime,
                    PathFlags = SubdocPathFlags.Xattr
                });
            }

            if (projectList.Count == 0 || specCount > 16)
            {
                // No projections or we have exceeded the max #fields returnable by sub-doc so fetch the whole doc
                specs.Add(new LookupInSpec
                {
                    Path = "",
                    OpCode = OpCode.Get,
                    DocFlags = SubdocDocFlags.None
                });
            }
            else
            {
                //Add the projections for fetching
                projectList.ForEach(path => specs.Add(new LookupInSpec
                {
                    OpCode = OpCode.SubGet,
                    Path = path
                }));
            }

            var lookupOp = await ExecuteLookupIn(id,
                    specs, new LookupInOptions().Timeout(options.TimeoutValue), rootSpan)
                .ConfigureAwait(false);
            rootSpan.OperationId(lookupOp);

            return new GetResult(lookupOp.ExtractBody(), lookupOp.Transcoder, _getLogger, specs, projectList)
            {
                Id = lookupOp.Key,
                Cas = lookupOp.Cas,
                OpCode = lookupOp.OpCode,
                Flags = lookupOp.Flags,
                Header = lookupOp.Header,
                Opaque = lookupOp.Opaque
            };
        }

        private async Task<Get<byte[]>> ExecuteGet(string id, GetOptions options, IInternalSpan span)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            var get = new Get<byte[]>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                Span = span
            };
            _operationConfigurator.Configure(get, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, get);
            await _bucket.RetryAsync(get, cts.Token).ConfigureAwait(false);
            return get;
        }

        #endregion

        #region Exists

        public async Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null)
        {
            try
            {
                //sanity check for deferred bootstrapping errors
                _bucket.ThrowIfBootStrapFailed();

                options ??= new ExistsOptions();

                using var rootSpan = RootSpan(OperationNames.GetMetaExists);
                using var getMetaOp = new GetMeta
                {
                    Key = id,
                    Cid = Cid,
                    CName = Name,
                    Span = rootSpan
                };
                _operationConfigurator.Configure(getMetaOp, options);

                using var cts = CreateRetryTimeoutCancellationTokenSource(options, getMetaOp);
                await _bucket.RetryAsync(getMetaOp, cts.Token).ConfigureAwait(false);
                var result = getMetaOp.GetValue();

                return new ExistsResult
                {
                    Cas = getMetaOp.Cas,
                    Exists = !result.Deleted
                };
            }
            catch (DocumentNotFoundException)
            {
                return new ExistsResult
                {
                    Exists = false
                };
            }
        }

        #endregion

        #region Upsert

        public async Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new UpsertOptions();
            using var rootSpan = RootSpan(OperationNames.SetUpsert);
            using var upsertOp = new Set<T>(_bucket.Name, id)
            {
                Content = content,
                CName = Name,
                SName = ScopeName,
                Cid = Cid,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };

            _operationConfigurator.Configure(upsertOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, upsertOp);
            await _bucket.RetryAsync(upsertOp, cts.Token).ConfigureAwait(false);
            return new MutationResult(upsertOp.Cas, null, upsertOp.MutationToken);
        }

        private CancellationTokenSource CreateRetryTimeoutCancellationTokenSource(ITimeoutOptions options, IOperation op) =>
            options.Token.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(options.Token)
                : new CancellationTokenSource(GetTimeout(options.Timeout, op));

        #endregion

        #region Insert

        public async Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new InsertOptions();
            using var rootSpan = RootSpan(OperationNames.AddInsert);
            using var insertOp = new Add<T>(_bucket.Name, id)
            {
                Content = content,
                Cid = Cid,
                CName = Name,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(insertOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, insertOp);
            await _bucket.RetryAsync(insertOp, cts.Token).ConfigureAwait(false);
            return new MutationResult(insertOp.Cas, null, insertOp.MutationToken);
        }

        #endregion

        #region Replace

        public async Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new ReplaceOptions();
            using var rootSpan = RootSpan(OperationNames.Replace);
            using var replaceOp = new Replace<T>(_bucket.Name, id)
            {
                Content = content,
                Cas = options.CasValue,
                Cid = Cid,
                CName = Name,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(replaceOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, replaceOp);
            await _bucket.RetryAsync(replaceOp, cts.Token).ConfigureAwait(false);
            return new MutationResult(replaceOp.Cas, null, replaceOp.MutationToken);
        }

        #endregion

        #region Remove

        public async Task RemoveAsync(string id, RemoveOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new RemoveOptions();
            using var rootSpan = RootSpan(OperationNames.DeleteRemove);
            using var removeOp = new Delete
            {
                Key = id,
                Cas = options.CasValue,
                Cid = Cid,
                CName = Name,
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan
            };
            _operationConfigurator.Configure(removeOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, removeOp);
            await _bucket.RetryAsync(removeOp, cts.Token).ConfigureAwait(false);
        }

        #endregion

        #region Unlock

        public async Task UnlockAsync<T>(string id, ulong cas, UnlockOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new UnlockOptions();
            using var rootSpan = RootSpan(OperationNames.Unlock);
            using var unlockOp = new Unlock
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                Cas = cas,
                Span = rootSpan
            };
            _operationConfigurator.Configure(unlockOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, unlockOp);
            await _bucket.RetryAsync(unlockOp, cts.Token).ConfigureAwait(false);
        }

        #endregion

        #region Touch

        public async Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new TouchOptions();
            using var rootSpan = RootSpan(OperationNames.Touch);
            using var touchOp = new Touch
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                Expires = expiry.ToTtl(),
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan
            };
            _operationConfigurator.Configure(touchOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, touchOp);
            await _bucket.RetryAsync(touchOp, cts.Token).ConfigureAwait(false);
        }

        #endregion

        #region GetAndTouch

        public async Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new GetAndTouchOptions();
            using var rootSpan = RootSpan(OperationNames.GetAndTouch);
            using var getAndTouchOp = new GetT<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                Expires = expiry.ToTtl(),
                Span = rootSpan
            };
            _operationConfigurator.Configure(getAndTouchOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, getAndTouchOp);
            await _bucket.RetryAsync(getAndTouchOp, cts.Token).ConfigureAwait(false);

            return new GetResult(getAndTouchOp.ExtractBody(), getAndTouchOp.Transcoder, _getLogger)
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

        public async Task<IGetResult> GetAndLockAsync(string id, TimeSpan lockTime, GetAndLockOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new GetAndLockOptions();
            using var rootSpan = RootSpan(OperationNames.GetAndLock);
            using var getAndLockOp = new GetL<byte[]>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                Expiry = lockTime.ToTtl(),
                Span = rootSpan
            };
            _operationConfigurator.Configure(getAndLockOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, getAndLockOp);
            await _bucket.RetryAsync(getAndLockOp, cts.Token).ConfigureAwait(false);
            return new GetResult(getAndLockOp.ExtractBody(), getAndLockOp.Transcoder, _getLogger)
            {
                Id = getAndLockOp.Key,
                Cas = getAndLockOp.Cas,
                Flags = getAndLockOp.Flags,
                Header = getAndLockOp.Header,
                OpCode = getAndLockOp.OpCode
            };
        }

        #endregion

        #region LookupIn

        public async Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs, LookupInOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            using var rootSpan = RootSpan(OperationNames.MultiLookupSubdocGet);
            options ??= new LookupInOptions();
            using var lookup = await ExecuteLookupIn(id, specs, options, rootSpan).ConfigureAwait(false);
            var responseStatus = lookup.Header.Status;
            var isDeleted = responseStatus == ResponseStatus.SubDocSuccessDeletedDocument ||
                            responseStatus == ResponseStatus.SubdocMultiPathFailureDeleted;
            return new LookupInResult(lookup.GetCommandValues(), lookup.Cas, null,
                options.SerializerValue ?? lookup.Transcoder.Serializer, isDeleted);
        }

        private async Task<MultiLookup<byte[]>> ExecuteLookupIn(string id, IEnumerable<LookupInSpec> specs,
            LookupInOptions options, IInternalSpan span)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            // convert new style specs into old style builder
            var builder = new LookupInBuilder<byte[]>(null, null, id, specs);

            //add the virtual xattr attribute to get the doc expiration time
            if (options.ExpiryValue)
            {
                builder.Get(VirtualXttrs.DocExpiryTime, SubdocPathFlags.Xattr);
            }

            var lookup = new MultiLookup<byte[]>
            {
                Key = id,
                Builder = builder,
                Cid = Cid,
                CName = Name,
                DocFlags = options.AccessDeletedValue ? SubdocDocFlags.AccessDeleted : SubdocDocFlags.None,
                Span = span
            };
            _operationConfigurator.Configure(lookup, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, lookup);
            await _bucket.RetryAsync(lookup, cts.Token).ConfigureAwait(false);
            return lookup;
        }

        #endregion

        #region MutateIn

        public async Task<IMutateInResult> MutateInAsync(string id, IEnumerable<MutateInSpec> specs, MutateInOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new MutateInOptions();
            // convert new style specs into old style builder
            var builder = new MutateInBuilder<byte[]>(null, null, id, specs);

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
                if (!_bucket.BucketConfig?.BucketCapabilities.Contains(BucketCapabilities.CREATE_AS_DELETED) == true)
                {
                    throw new FeatureNotAvailableException(nameof(BucketCapabilities.CREATE_AS_DELETED));
                }

                docFlags |= SubdocDocFlags.CreateAsDeleted;
            }

            if (options.AccessDeletedValue)
            {
                docFlags |= SubdocDocFlags.AccessDeleted;
            }

            using var rootSpan = RootSpan(OperationNames.MultiMutationSubdocMutate);
            using var mutation = new MultiMutation<byte[]>
            {
                Key = id,
                BucketName = _bucket.Name,
                Builder = builder,
                Cid = Cid,
                CName = Name,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DocFlags = docFlags,
                Span = rootSpan
            };
            _operationConfigurator.Configure(mutation, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, mutation);
            await _bucket.RetryAsync(mutation, cts.Token).ConfigureAwait(false);

            return new MutateInResult(mutation.GetCommandValues(), mutation.Cas, mutation.MutationToken,
                options.SerializerValue ?? mutation.Transcoder.Serializer);
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

        public async Task<IMutationResult> AppendAsync(string id, byte[] value, AppendOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new AppendOptions();
            using var rootSpan = RootSpan(OperationNames.Append);
            using var op = new Append<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(op, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op);
            await _bucket.RetryAsync(op, cts.Token).ConfigureAwait(false);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Prepend

        public async Task<IMutationResult> PrependAsync(string id, byte[] value, PrependOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new PrependOptions();
            using var rootSpan = RootSpan(OperationNames.Prepend);
            using var op = new Prepend<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(op, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op);
            await _bucket.RetryAsync(op, cts.Token).ConfigureAwait(false);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Increment

        public async Task<ICounterResult> IncrementAsync(string id, IncrementOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new IncrementOptions();
            using var rootSpan = RootSpan(OperationNames.Increment);
            using var op = new Increment(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                Delta = options.DeltaValue,
                Initial = options.InitialValue,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Expires = options.ExpiryValue.ToTtl()
            };
            _operationConfigurator.Configure(op, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op);
            await _bucket.RetryAsync(op, cts.Token).ConfigureAwait(false);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Decrement

        public async Task<ICounterResult> DecrementAsync(string id, DecrementOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new DecrementOptions();
            using var rootSpan = RootSpan(OperationNames.Decrement);
            using var op = new Decrement(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                Delta = options.DeltaValue,
                Initial = options.InitialValue,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan,
                Expires = options.ExpiryValue.ToTtl()
            };
            _operationConfigurator.Configure(op, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op);
            await _bucket.RetryAsync(op, cts.Token).ConfigureAwait(false);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion

        #region GetAnyReplica / GetAllReplicas

        public async Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            using var rootSpan = RootSpan(OperationNames.GetAnyReplica);
            options ??= new GetAnyReplicaOptions();
            var vBucket = (VBucket) _bucket.KeyMapper!.MapKey(id);

            if (!vBucket.HasReplicas)
            {
                Logger.LogWarning($"Call to GetAnyReplica for key [{id}] but none are configured. Only the active document will be retrieved.");
            }

            // get primary
            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1)
            {
                GetPrimary(id, rootSpan, options.TokenValue, options)
            };

            // get replicas
            tasks.AddRange(vBucket.Replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, options)));

            return await Task.WhenAny(tasks).ConfigureAwait(false).GetAwaiter().GetResult();//TODO BUG!
        }

        public IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            using var rootSpan = RootSpan(OperationNames.GetAllReplicas);
            options ??= new GetAllReplicasOptions();
            var vBucket = (VBucket) _bucket.KeyMapper!.MapKey(id);
            if (!vBucket.HasReplicas)
            {
                Logger.LogWarning($"Call to GetAllReplicas for key [{id}] but none are configured. Only the active document will be retrieved.");
            }

            // get primary
            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1)
            {
                GetPrimary(id, rootSpan, options.TokenValue, options)
            };

            // get replicas
            tasks.AddRange(vBucket.Replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, options)));

            return tasks;
        }

        private async Task<IGetReplicaResult> GetPrimary(string id, IInternalSpan span, CancellationToken cancellationToken, ITranscoderOverrideOptions options)
        {
            using var childSpan = _tracer.InternalSpan(OperationNames.Get, span);
            using var getOp = new Get<object>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                Span = childSpan
            };
            _operationConfigurator.Configure(getOp, options);

            await _bucket.RetryAsync(getOp, cancellationToken).ConfigureAwait(false);
            return new GetReplicaResult(getOp.ExtractBody(), getOp.Transcoder, _getLogger)
            {
                Id = getOp.Key,
                Cas = getOp.Cas,
                OpCode = getOp.OpCode,
                Flags = getOp.Flags,
                Header = getOp.Header,
                IsActive = true
            };
        }

        private async Task<IGetReplicaResult> GetReplica(string id, short index, IInternalSpan span, CancellationToken cancellationToken, ITranscoderOverrideOptions options)
        {
            using var childSpan = _tracer.InternalSpan(OperationNames.ReplicaRead, span);
            using var getOp = new ReplicaRead<object>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                ReplicaIdx = index,
                Span = childSpan
            };
            _operationConfigurator.Configure(getOp, options);

            await _bucket.RetryAsync(getOp, cancellationToken).ConfigureAwait(false);
            return new GetReplicaResult(getOp.ExtractBody(), getOp.Transcoder, _getLogger)
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

        private IInternalSpan RootSpan(string operation) =>
            _tracer.RootSpan(RequestTracing.ServiceIdentifier.Kv, operation);
    }
}
