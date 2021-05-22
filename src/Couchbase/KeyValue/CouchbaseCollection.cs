using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Collections;
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
    internal class CouchbaseCollection : ICouchbaseCollection, IBinaryCollection, IInternalCollection
    {
        public const string DefaultCollectionName = "_default";
        private readonly BucketBase _bucket;
        private readonly ILogger<GetResult> _getLogger;
        private readonly IOperationConfigurator _operationConfigurator;
        private readonly IRequestTracer _tracer;
        private readonly ITypeTranscoder _rawStringTranscoder = new RawStringTranscoder();
        private static readonly SemaphoreSlim CidLock = new(1);

        internal CouchbaseCollection(BucketBase bucket, IOperationConfigurator operationConfigurator,
            ILogger<CouchbaseCollection> logger,
            ILogger<GetResult> getLogger, IRedactor redactor,
            string name, IScope scope, IRequestTracer tracer)
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
        }

        internal IRedactor Redactor { get; }

        public string ScopeName => Scope.Name;

        public uint? Cid { get; set; }

        public ILogger<CouchbaseCollection> Logger { get; }

        public string Name { get; }

        /// <inheritdoc />
        public IScope Scope { get; }

        public IBinaryCollection Binary => this;

        #region Get

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

            // TODO: Since we're actually using LookupIn for Get requests, which operation name should we use?
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Get);
            options ??= GetOptions.Default;

            var projectList = options.ProjectListValue;

            var specCount = projectList.Count;
            if (options.IncludeExpiryValue) specCount++;

            if (specCount == 0)
            {
                // We aren't including the expiry value and we have no projections so fetch the whole doc using a Get operation
                var getOp = new Get<byte[]>
                {
                    Key = id,
                    Cid = Cid,
                    CName = Name,
                    SName = ScopeName,
                    Span = rootSpan
                };
                _operationConfigurator.Configure(getOp, options);

                using var cts = CreateRetryTimeoutCancellationTokenSource(options, getOp, out var tokenPair);
                await _bucket.RetryAsync(getOp, tokenPair).ConfigureAwait(false);

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
                    .Transcoder(options.TranscoderValue)
                : LookupInOptions.Default;

            var lookupOp = await ExecuteLookupIn(id,
                    specs, lookupInOptions, rootSpan)
                .ConfigureAwait(false);
            rootSpan.WithOperationId(lookupOp);
            rootSpan.Dispose();
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

        #endregion

        #region Exists

        public async Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null)
        {
            try
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

                using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetMetaExists);
                using var getMetaOp = new GetMeta
                {
                    Key = id,
                    Cid = Cid,
                    CName = Name,
                    SName = ScopeName,
                    Span = rootSpan
                };
                _operationConfigurator.Configure(getMetaOp, options);

                using var cts = CreateRetryTimeoutCancellationTokenSource(options, getMetaOp, out var tokenPair);
                await _bucket.RetryAsync(getMetaOp, tokenPair).ConfigureAwait(false);
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

        #region Insert

        public async Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= InsertOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.AddInsert);
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

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, insertOp, out var tokenPair);
            await _bucket.RetryAsync(insertOp, tokenPair).ConfigureAwait(false);
            return new MutationResult(insertOp.Cas, null, insertOp.MutationToken);
        }

        #endregion

        #region Replace

        public async Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= ReplaceOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Replace);
            using var replaceOp = new Replace<T>(_bucket.Name, id)
            {
                Content = content,
                Cas = options.CasValue,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Expires = options.ExpiryValue.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(replaceOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, replaceOp, out var tokenPair);
            await _bucket.RetryAsync(replaceOp, tokenPair).ConfigureAwait(false);
            return new MutationResult(replaceOp.Cas, null, replaceOp.MutationToken);
        }

        #endregion

        #region Remove

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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.DeleteRemove);
            using var removeOp = new Delete
            {
                Key = id,
                Cas = options.CasValue,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan
            };
            _operationConfigurator.Configure(removeOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, removeOp, out var tokenPair);
            await _bucket.RetryAsync(removeOp, tokenPair).ConfigureAwait(false);
        }

        #endregion

        #region Unlock

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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Unlock);
            using var unlockOp = new Unlock
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Cas = cas,
                Span = rootSpan
            };
            _operationConfigurator.Configure(unlockOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, unlockOp, out var tokenPair);
            await _bucket.RetryAsync(unlockOp, tokenPair).ConfigureAwait(false);
        }

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
                Span = rootSpan
            };
            _operationConfigurator.Configure(unlockOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, unlockOp, out var tokenPair);
            await _bucket.RetryAsync(unlockOp, tokenPair).ConfigureAwait(false);
        }

        #endregion

        #region Touch

        public async Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null)
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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Touch);
            using var touchOp = new Touch
            {
                Key = id,
                Cid = Cid,
                SName = ScopeName,
                CName = Name,
                Expires = expiry.ToTtl(),
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Span = rootSpan
            };
            _operationConfigurator.Configure(touchOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, touchOp, out var tokenPair);
            await _bucket.RetryAsync(touchOp, tokenPair).ConfigureAwait(false);
        }

        #endregion

        #region GetAndTouch

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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndTouch);
            using var getAndTouchOp = new GetT<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Expires = expiry.ToTtl(),
                Span = rootSpan
            };
            _operationConfigurator.Configure(getAndTouchOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, getAndTouchOp, out var tokenPair);
            await _bucket.RetryAsync(getAndTouchOp, tokenPair).ConfigureAwait(false);

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

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= GetAndLockOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAndLock);
            using var getAndLockOp = new GetL<byte[]>
            {
                Key = id,
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Expiry = lockTime.ToTtl(),
                Span = rootSpan
            };
            _operationConfigurator.Configure(getAndLockOp, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, getAndLockOp, out var tokenPair);
            await _bucket.RetryAsync(getAndLockOp, tokenPair).ConfigureAwait(false);
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

        #region Upsert

        public async Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            options ??= UpsertOptions.Default;
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.SetUpsert);
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

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, upsertOp, out var tokenPair);
            await _bucket.RetryAsync(upsertOp, tokenPair).ConfigureAwait(false);
            return new MutationResult(upsertOp.Cas, null, upsertOp.MutationToken);
        }

        private CancellationTokenSource CreateRetryTimeoutCancellationTokenSource(
            ITimeoutOptions options, IOperation op, out CancellationTokenPair tokenPair)
        {
            var cts = new CancellationTokenSource(GetTimeout(options.Timeout, op));
            tokenPair = new CancellationTokenPair(options.Token, cts.Token);
            return cts;
        }

        #endregion

        #region LookupIn

        public async Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs,
            LookupInOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //Check to see if the CID is needed
            if (RequiresCid())
            {
                //Get the collection ID
                await PopulateCidAsync().ConfigureAwait(false);
            }

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.LookupIn);
            options ??= LookupInOptions.Default;
            using var lookup = await ExecuteLookupIn(id, specs, options, rootSpan).ConfigureAwait(false);
            var responseStatus = lookup.Header.Status;
            var isDeleted = responseStatus == ResponseStatus.SubDocSuccessDeletedDocument ||
                            responseStatus == ResponseStatus.SubdocMultiPathFailureDeleted;
            return new LookupInResult(lookup.GetCommandValues(), lookup.Cas, null,
                options.SerializerValue ?? lookup.Transcoder.Serializer, isDeleted);
        }

        private async Task<MultiLookup<byte[]>> ExecuteLookupIn(string id, IEnumerable<LookupInSpec> specs,
            LookupInOptions options, IRequestSpan span)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            //Get the collection ID
            await PopulateCidAsync().ConfigureAwait(false);

            //add the virtual xattr attribute to get the doc expiration time
            if (options.ExpiryValue)
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

            var lookup = new MultiLookup<byte[]>(id, specs)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                DocFlags = options.AccessDeletedValue ? SubdocDocFlags.AccessDeleted : SubdocDocFlags.None,
                Span = span
            };
            _operationConfigurator.Configure(lookup, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, lookup, out var tokenPair);
            await _bucket.RetryAsync(lookup, tokenPair).ConfigureAwait(false);
            return lookup;
        }

        #endregion

        #region MutateIn

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
                    throw new FeatureNotAvailableException(nameof(BucketCapabilities.CREATE_AS_DELETED));

                docFlags |= SubdocDocFlags.CreateAsDeleted;
            }

            if (options.AccessDeletedValue) docFlags |= SubdocDocFlags.AccessDeleted;

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.MutateIn);
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
                Span = rootSpan
            };
            _operationConfigurator.Configure(mutation, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, mutation, out var tokenPair);
            await _bucket.RetryAsync(mutation, tokenPair).ConfigureAwait(false);

#pragma warning disable 618 // MutateInResult is marked obsolete until it is made internal
            return new MutateInResult(mutation.GetCommandValues(), mutation.Cas, mutation.MutationToken,
                options.SerializerValue ?? mutation.Transcoder.Serializer);
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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Append);
            using var op = new Append<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(op, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op, out var tokenPair);
            await _bucket.RetryAsync(op, tokenPair).ConfigureAwait(false);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Prepend

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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Prepend);
            using var op = new Prepend<byte[]>(_bucket.Name, id)
            {
                Cid = Cid,
                CName = Name,
                SName = ScopeName,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Span = rootSpan
            };
            _operationConfigurator.Configure(op, options);

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op, out var tokenPair);
            await _bucket.RetryAsync(op, tokenPair).ConfigureAwait(false);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Increment

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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Increment);
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

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op, out var tokenPair);
            await _bucket.RetryAsync(op, tokenPair).ConfigureAwait(false);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Decrement

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
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.Decrement);
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

            using var cts = CreateRetryTimeoutCancellationTokenSource(options, op, out var tokenPair);
            await _bucket.RetryAsync(op, tokenPair).ConfigureAwait(false);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion

        #region GetAnyReplica / GetAllReplicas

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

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAnyReplica);
            options ??= GetAnyReplicaOptions.Default;
            var vBucket = (VBucket) _bucket.KeyMapper!.MapKey(id);

            if (!vBucket.HasReplicas)
                Logger.LogWarning(
                    $"Call to GetAnyReplica for key [{id}] but none are configured. Only the active document will be retrieved.");

            // get primary
            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1)
            {
                GetPrimary(id, rootSpan, options.TokenValue, options)
            };

            // get replicas
            tasks.AddRange(
                vBucket.Replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, options)));

            return await Task.WhenAny(tasks).ConfigureAwait(false).GetAwaiter().GetResult(); //TODO BUG!
        }

        public IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id,
            GetAllReplicasOptions? options = null)
        {
            //sanity check for deferred bootstrapping errors
            _bucket.ThrowIfBootStrapFailed();

            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Kv.GetAllReplicas);
            options ??= GetAllReplicasOptions.Default;
            var vBucket = (VBucket) _bucket.KeyMapper!.MapKey(id);
            if (!vBucket.HasReplicas)
                Logger.LogWarning(
                    $"Call to GetAllReplicas for key [{id}] but none are configured. Only the active document will be retrieved.");

            // get primary
            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1)
            {
                GetPrimary(id, rootSpan, options.TokenValue, options)
            };

            // get replicas
            tasks.AddRange(
                vBucket.Replicas.Select(index => GetReplica(id, index, rootSpan, options.TokenValue, options)));

            return tasks;
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

            using var cts =
                CreateRetryTimeoutCancellationTokenSource((ITimeoutOptions) options, getOp, out var tokenPair);
            await _bucket.RetryAsync(getOp, tokenPair).ConfigureAwait(false);
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

            using var cts =
                CreateRetryTimeoutCancellationTokenSource((ITimeoutOptions) options, getOp, out var tokenPair);
            await _bucket.RetryAsync(getOp, tokenPair).ConfigureAwait(false);
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

        #region GET_CID

        /// <summary>
        /// Servers 7.0 and above support collections and require the CID to be fetched.
        /// Earlier versions of the server may support collections in dev-preview mode so
        /// we check to see if its been enabled via the results of the HELLO command.
        /// </summary>
        /// <returns>true if the server supports collections and the CID is null.</returns>
        private bool RequiresCid()
        {
            return _bucket.Context.SupportsCollections && !Cid.HasValue;
        }

        private async ValueTask PopulateCidAsync()
        {
            Logger.LogDebug("Fetching CID for {scope}.{collection}", ScopeName, Name);
            var waitedSuccessfully = await CidLock.WaitAsync(2500);
            try
            {
                if (!waitedSuccessfully)
                {
                    throw new AmbiguousTimeoutException($"Timed out waiting for GET_CID in {ScopeName}.{Name}");
                }
                if (!_bucket.Context.SupportsCollections || Cid.HasValue)
                {
                    return;
                }

                //for later cheshire cat builds
                Cid = await GetCidAsync($"{ScopeName}.{Name}", true);
            }
            catch (Exception e)
            {
                Logger.LogInformation(e, "Possible non-terminal error fetching CID. Cluster maybe in Dev-Preview mode.");
                if (e is InvalidArgumentException)
                    try
                    {
                        //if this is encountered were on a older server pre-cheshire cat changes
                        Cid = await GetCidAsync($"{ScopeName}.{Name}", false);
                    }
                    catch (UnsupportedException)
                    {
                        //an older server without collections enabled
                        Logger.LogInformation("Collections are not supported on this server version.");
                    }
                else
                {
                    throw;
                }
            }
            finally
            {
                CidLock.Release();
            }

            Logger.LogDebug("Completed fetching CID for {scope}.{collection}", ScopeName, Name);
        }

        /// <summary>
        /// Sends the scope/collection in the key or the operation body as content based on the flag.
        /// </summary>
        /// <param name="fullyQualifiedName">The fully qualified scope.collection name.</param>
        /// <param name="sendAsBody">true to send as the body; false in the key for dev-preview (pre-7.0 servers). </param>
        /// <returns></returns>
        private async Task<uint?> GetCidAsync(string fullyQualifiedName, bool sendAsBody)
        {
            using var rootSpan = RootSpan(OuterRequestSpans.ServiceSpan.Internal.GetCid);
            using var getCid = new GetCid
            {
                Opaque = SequenceGenerator.GetNext(),
                Span = rootSpan,
            };

            if (sendAsBody)
            {
                getCid.Content = fullyQualifiedName;
            }
            else
            {
                getCid.Key = fullyQualifiedName;
            }

            _operationConfigurator.Configure(getCid, new GetOptions().Transcoder(_rawStringTranscoder));
            await _bucket.RetryAsync(getCid).ConfigureAwait(false);
            var resultWithValue = getCid.GetValueAsUint();
            return resultWithValue;
        }
        #endregion

        #region tracing
        private IRequestSpan RootSpan(string operation)
        {
            var span = _tracer.RequestSpan(operation);
            span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
            span.SetAttribute(OuterRequestSpans.Attributes.Service, nameof(OuterRequestSpans.ServiceSpan.Kv).ToLowerInvariant());
            span.SetAttribute(OuterRequestSpans.Attributes.BucketName, _bucket.Name);
            span.SetAttribute(OuterRequestSpans.Attributes.ScopeName, ScopeName);
            span.SetAttribute(OuterRequestSpans.Attributes.CollectionName, Name);
            span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            return span;
        }
        #endregion
    }
}
