using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
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
        private readonly ITypeTranscoder _transcoder;
        private readonly ILogger<GetResult> _getLogger;
        private readonly IRequestTracer _tracer;

        internal CouchbaseCollection(BucketBase bucket, ITypeTranscoder transcoder, ILogger<CouchbaseCollection> logger,
            ILogger<GetResult> getLogger, IRedactor redactor,
            uint? cid, string name, IScope scope, IRequestTracer tracer)
        {
            Cid = cid;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
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

            var projectList = options.ProjectListValue;
            if (projectList.Any())
            {
                //we have succeeded the max #fields returnable by sub-doc so fetch the whole doc
                if (projectList.Count + specs.Count > 16)
                {
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
                    projectList.ForEach(path=>specs.Add(new LookupInSpec
                    {
                        OpCode = OpCode.SubGet,
                        Path = path
                    }));
                }
            }
            else
            {
                //Project list is empty so fetch the whole doc
                specs.Add(new LookupInSpec
                {
                    Path = "",
                    OpCode = OpCode.Get,
                    DocFlags = SubdocDocFlags.None
                });
            }

            var lookupOp = await ExecuteLookupIn(id,
                    specs, new LookupInOptions().Timeout(options.TimeoutValue), rootSpan)
                .ConfigureAwait(false);
            rootSpan.OperationId(lookupOp);
            var transcoder = options.TranscoderValue ?? _transcoder;

            return new GetResult(lookupOp.ExtractData(), transcoder, _getLogger, specs, projectList)
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
                    Transcoder = _transcoder,
                    Span = rootSpan
                };
                await _bucket.RetryAsync(getMetaOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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
            var transcoder = options.TranscoderValue ?? _transcoder;
            using var rootSpan = RootSpan(OperationNames.SetUpsert);
            using var upsertOp = new Set<T>(_bucket.Name, id)
            {
                Content = content,
                CName = Name,
                SName = ScopeName,
                Cid = Cid,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(upsertOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
            return new MutationResult(upsertOp.Cas, null, upsertOp.MutationToken);
        }

        #endregion

        #region Insert

        public async Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new InsertOptions();
            var transcoder = options.TranscoderValue ?? _transcoder;
            using var rootSpan = RootSpan(OperationNames.AddInsert);
            using var insertOp = new Add<T>(_bucket.Name, id)
            {
                Content = content,
                Cid = Cid,
                CName = Name,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(insertOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
            return new MutationResult(insertOp.Cas, null, insertOp.MutationToken);
        }

        #endregion

        #region Replace

        public async Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new ReplaceOptions();
            var transcoder = options.TranscoderValue ?? _transcoder;
            using var rootSpan = RootSpan(OperationNames.Replace);
            using var replaceOp = new Replace<T>(_bucket.Name, id)
            {
                Content = content,
                Cas = options.CasValue,
                Cid = Cid,
                CName = Name,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(replaceOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);

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
                Transcoder = _transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(removeOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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
                Transcoder = _transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(unlockOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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
                Transcoder = _transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(touchOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
        }

        #endregion

        #region GetAndTouch

        public async Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            options ??= new GetAndTouchOptions();
            var transcoder = options.TranscoderValue ?? _transcoder;
            using var rootSpan = RootSpan(OperationNames.GetAndTouch);
            using var getAndTouchOp = new GetT<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                Expires = expiry.ToTtl(),
                Transcoder = transcoder,
                Span = rootSpan
            };

            await _bucket.RetryAsync(getAndTouchOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
            return new GetResult(getAndTouchOp.ExtractData(), transcoder, _getLogger)
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
            var transcoder = options.TranscoderValue ?? _transcoder;
            using var rootSpan = RootSpan(OperationNames.GetAndLock);
            using var getAndLockOp = new GetL<byte[]>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                Expiry = lockTime.ToTtl(),
                Transcoder = transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(getAndLockOp, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
            return new GetResult(getAndLockOp.ExtractData(), transcoder, _getLogger)
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
            return new LookupInResult(lookup.GetCommandValues(), lookup.Cas, null,
                options.SerializerValue ?? _transcoder.Serializer);
        }

        private async Task<MultiLookup<byte[]>> ExecuteLookupIn(string id, IEnumerable<LookupInSpec> specs,
            LookupInOptions options, IInternalSpan span)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            // convert new style specs into old style builder
            var builder = new LookupInBuilder<byte[]>(null, null, id, specs);

            //add the virtual xttar attribute to get the doc expiration time
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
                Transcoder = _transcoder,
                Span = span
            };
            await _bucket.RetryAsync(lookup, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            using var rootSpan = RootSpan(OperationNames.MultiMutationSubdocMutate);
            using var mutation = new MultiMutation<byte[]>
            {
                Key = id,
                BucketName = _bucket.Name,
                Builder = builder,
                Cid = Cid,
                CName = Name,
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = _transcoder,
                DocFlags = docFlags,
                Span = rootSpan
            };
            await _bucket.RetryAsync(mutation, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);

            return new MutateInResult(mutation.GetCommandValues(),mutation.Cas, mutation.MutationToken,
                options.SerializerValue ?? _transcoder.Serializer);
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
                Transcoder = _transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(op, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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
                Transcoder = _transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(op, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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
                Transcoder = _transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(op, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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
                Transcoder = _transcoder,
                Span = rootSpan
            };
            await _bucket.RetryAsync(op, options.TokenValue, options.TimeoutValue).ConfigureAwait(false);
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

            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1);

            var transcoder = options.TranscoderValue ?? _transcoder;

            // get primary
            tasks.Add(GetPrimary(id, rootSpan, options.TokenValue, transcoder));

            // get replicas
            tasks.AddRange(vBucket.Replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, transcoder)));

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

            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1);

            var transcoder = options.TranscoderValue ?? _transcoder;

            // get primary
            tasks.Add(GetPrimary(id, rootSpan, options.TokenValue, transcoder));

            // get replicas
            tasks.AddRange(vBucket.Replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, transcoder)));

            return tasks;
        }

        private async Task<IGetReplicaResult> GetPrimary(string id, IInternalSpan span, CancellationToken cancellationToken, ITypeTranscoder transcoder)
        {
            using var childSpan = _tracer.InternalSpan(OperationNames.Get, span);
            using var getOp = new Get<object>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                Transcoder = transcoder,
                Span = childSpan
            };

            await _bucket.RetryAsync(getOp, cancellationToken).ConfigureAwait(false);
            return new GetReplicaResult(getOp.ExtractData(), transcoder, _getLogger)
            {
                Id = getOp.Key,
                Cas = getOp.Cas,
                OpCode = getOp.OpCode,
                Flags = getOp.Flags,
                Header = getOp.Header,
                IsActive = true
            };
        }

        private async Task<IGetReplicaResult> GetReplica(string id, short index, IInternalSpan span, CancellationToken cancellationToken, ITypeTranscoder transcoder)
        {
            using var childSpan = _tracer.InternalSpan(OperationNames.ReplicaRead, span);
            using var getOp = new ReplicaRead<object>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                ReplicaIdx = index,
                Transcoder = transcoder,
                Span = childSpan
            };

            await _bucket.RetryAsync(getOp, cancellationToken).ConfigureAwait(false);
            return new GetReplicaResult(getOp.ExtractData(), transcoder, _getLogger)
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
            _tracer.RootSpan(CouchbaseTags.ServiceKv, operation);
    }
}
