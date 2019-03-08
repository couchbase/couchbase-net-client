using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations.Legacy.SubDocument;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase
{
    public class CouchbaseCollection : ICollection
    {
        internal const string DefaultCollection = "_default";
        private readonly IBucketSender _bucket;
        private static readonly TimeSpan DefaultTimeout = new TimeSpan(0,0,0,0,2500);//temp
        private ITypeTranscoder _transcoder = new DefaultTranscoder(new DefaultConverter());

        public CouchbaseCollection(IBucket bucket, string cid, string name, IBinaryCollection binaryCollection = null)
        {
            Cid = Convert.ToUInt32(cid);
            Name = name;
            Binary = binaryCollection;
            _bucket = (IBucketSender) bucket;
        }

        public uint Cid { get; }

        public string Name { get; }

        public IBinaryCollection Binary { get; }

        private async Task ExecuteOp(IOperation op, CancellationToken token = default(CancellationToken), TimeSpan? timeout = null)
        {
            // wire up op's completed function
            var tcs = new TaskCompletionSource<byte[]>();
            op.Completed = state =>
            {
                if (state.Status == ResponseStatus.Success)
                {
                    tcs.SetResult(state.Data.ToArray());
                }
                else
                {
                    tcs.SetException(new Exception(state.Status.ToString()));
                }

                return tcs.Task;
            };

            CancellationTokenSource cts = null;
            if (token == CancellationToken.None)
            {
                cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(timeout.HasValue && timeout != TimeSpan.Zero ? timeout.Value : DefaultTimeout);
                token = cts.Token;
            }

            using (token.Register(() =>
            {
                if (tcs.Task.Status != TaskStatus.RanToCompletion)
                {
                    tcs.SetCanceled();
                }
            }, useSynchronizationContext: false))
            {
                await _bucket.Send(op, tcs).ConfigureAwait(false);
                var bytes = await tcs.Task.ConfigureAwait(false);
                await op.ReadAsync(bytes).ConfigureAwait(false);

                //clean up the token if we used a default token
                cts?.Dispose();
            }
        }

        public Task<IGetResult> Get(string id, IEnumerable<string> projections = null, TimeSpan? timeout = null,
            CancellationToken token = default(CancellationToken))
        {
            var options = new GetOptions
            {
                ProjectList = projections?.ToList(),
                Timeout = timeout,
                Token = token
            };

            return Get(id, options);
        }

        public Task<IGetResult> Get(string id, Action<GetOptions> optionsAction)
        {
            var options = new GetOptions();
            optionsAction(options);

            return Get(id, options);
        }

        public async Task<IGetResult> Get(string id, GetOptions options)
        {
            //A projection operation
            var enumerable = options.ProjectList ?? new List<string>();
            if (enumerable.Any() && enumerable.Count < 16)
            {
                var specs = enumerable.Select(path => new OperationSpec
                {
                    OpCode = OpCode.SubGet,
                    Path = path
                }).ToList();

                if (!options.Timeout.HasValue)
                {
                    options.Timeout = DefaultTimeout;
                }

                var lookupOp = await ExecuteLookupIn(id, specs, new LookupInOptions().Timeout(options.Timeout.Value));
                return new GetResult(lookupOp.Data.ToArray(), _transcoder, specs)
                {
                    Id = lookupOp.Key,
                    Cas = lookupOp.Cas,
                    OpCode = lookupOp.OpCode,
                    Flags = lookupOp.Flags,
                    Header = lookupOp.Header
                };
            }

            //A regular get operation
            var getOp = new Get<object>
            {
                Key = id,
                Cid = Cid
            };

            await ExecuteOp(getOp, options.Token, options.Timeout).ConfigureAwait(false);
            return new GetResult(getOp.Data.ToArray(), _transcoder, null)
            {
                Id = getOp.Key,
                Cas = getOp.Cas,
                OpCode = getOp.OpCode,
                Flags = getOp.Flags,
                Header = getOp.Header
            };
        }

        public Task<IExistsResult> Exists(string id, TimeSpan? timeout = null, CancellationToken token = default(CancellationToken))
        {
            var options = new ExistsOptions
            {
                Timeout = timeout,
                Token = token
            };

            return Exists(id, options);
        }

        public Task<IExistsResult> Exists(string id, Action<ExistsOptions> optionsAction)
        {
            var options = new ExistsOptions();
            optionsAction(options);

            return Exists(id, options);
        }

        public async Task<IExistsResult> Exists(string id, ExistsOptions options)
        {
            var existsOp = new Observe
            {
                Key = id,
                Cid = Cid
            };

            try
            {
                await ExecuteOp(existsOp, options.Token, options.Timeout);
                var keyState = existsOp.GetValue().KeyState;
                return new ExistsResult
                {
                    Exists = existsOp.Success && keyState != KeyState.NotFound && keyState != KeyState.LogicalDeleted,
                    Cas = existsOp.Cas,
                    Expiration = TimeSpan.FromMilliseconds(existsOp.Expires)
                };
            }
            catch (KeyNotFoundException)
            {
                return new ExistsResult
                {
                    Exists = false
                };
            }
        }

        public Task<IMutationResult> Upsert<T>(string id, T content, TimeSpan? timeout = null, TimeSpan expiration = default(TimeSpan),
            ulong cas = 0, PersistTo persistTo = PersistTo.None, ReplicateTo replicateTo = ReplicateTo.None,
            DurabilityLevel durabilityLevel = DurabilityLevel.None, CancellationToken token = default(CancellationToken))
        {
            var options = new UpsertOptions
            {
                Timeout = timeout,
                Expiration = expiration,
                Cas = cas, PersistTo = persistTo,
                ReplicateTo = replicateTo,
                DurabilityLevel = durabilityLevel,
                Token = token
            };

            return Upsert(id, content, options);
        }

        public Task<IMutationResult> Upsert<T>(string id, T content, Action<UpsertOptions> optionsAction)
        {
            var options = new UpsertOptions();
            optionsAction(options);

            return Upsert(id, content, options);
        }

        public async Task<IMutationResult> Upsert<T>(string id, T content, UpsertOptions options)
        {
            var upsertOp = new Set<T>
            {
                Key = id,
                Content = content,
                Cas = options.Cas,
                Cid = Cid,
                Expires = options.Expiration.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500)
            };

            await ExecuteOp(upsertOp, options.Token, options.Timeout).ConfigureAwait(false);
            return new MutationResult(upsertOp.Cas, null, upsertOp.MutationToken);
        }

        public Task<IMutationResult> Insert<T>(string id, T content, TimeSpan? timeout = null, TimeSpan expiration = default(TimeSpan),
            ulong cas = 0, PersistTo persistTo = PersistTo.None, ReplicateTo replicateTo = ReplicateTo.None,
            DurabilityLevel durabilityLevel = DurabilityLevel.None, CancellationToken token = default(CancellationToken))
        {
            var options = new InsertOptions
            {
                Timeout = timeout,
                Expiration = expiration,
                Cas = cas, PersistTo = persistTo,
                ReplicateTo = replicateTo,
                DurabilityLevel = durabilityLevel,
                Token = token
            };

            return Insert(id, content, options);
        }

        public Task<IMutationResult> Insert<T>(string id, T content, Action<InsertOptions> optionsAction)
        {
            var options = new InsertOptions();
            optionsAction(options);

            return Insert(id, content, options);
        }

        public async Task<IMutationResult> Insert<T>(string id, T content, InsertOptions options)
        {
            var insertOp = new Add<T>
            {
                Key = id,
                Content = content,
                Cas = options.Cas,
                Cid = Cid,
                Expires = options.Expiration.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500)
            };
  
            await ExecuteOp(insertOp, options.Token, options.Timeout).ConfigureAwait(false);
            return new MutationResult(insertOp.Cas, null, insertOp.MutationToken);
        }

        public Task<IMutationResult> Replace<T>(string id, T content, TimeSpan? timeout = null, TimeSpan expiration = default(TimeSpan),
            ulong cas = 0, PersistTo persistTo = PersistTo.None, ReplicateTo replicateTo = ReplicateTo.None,
            DurabilityLevel durabilityLevel = DurabilityLevel.None, CancellationToken token = default(CancellationToken))
        {
            var options = new ReplaceOptions
            {
                Timeout = timeout,
                Expiration = expiration,
                Cas = cas,
                PersistTo = persistTo,
                ReplicateTo = replicateTo,
                DurabilityLevel = durabilityLevel,
                Token = token
            };

            return Replace(id, content, options);
        }

        public Task<IMutationResult> Replace<T>(string id, T content, Action<ReplaceOptions> optionsAction)
        {
            var options = new ReplaceOptions();
            optionsAction(options);

            return Replace(id, content, options);
        }

        public async Task<IMutationResult> Replace<T>(string id, T content, ReplaceOptions options)
        {
            var replaceOp = new Replace<T>
            {
                Key = id,
                Content = content,
                Cas = options.Cas,
                Cid = Cid,
                Expires = options.Expiration.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500)
            };

            await ExecuteOp(replaceOp, options.Token, options.Timeout).ConfigureAwait(false);
            return new MutationResult(replaceOp.Cas, null, replaceOp.MutationToken);
        }

        public Task Remove(string id, TimeSpan? timeout = null, ulong cas = 0,
            PersistTo persistTo = PersistTo.None, ReplicateTo replicateTo = ReplicateTo.None,
            DurabilityLevel durabilityLevel = DurabilityLevel.None, CancellationToken token = default(CancellationToken))
        {
            var options = new RemoveOptions
            {
                Timeout = timeout,
                Cas = cas,
                PersistTo = persistTo,
                ReplicateTo = replicateTo,
                DurabilityLevel = durabilityLevel,
                Token = token
            };

            return Remove(id, options);
        }

        public Task Remove(string id, Action<RemoveOptions> optionsAction)
        {
            var options = new RemoveOptions();
            optionsAction(options);

            return Remove(id, options);
        }

        public async Task Remove(string id, RemoveOptions options)
        {
            var removeOp = new Delete
            {
                Key = id,
                Cas = options.Cas,
                Cid = Cid,
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500)
            };

            await ExecuteOp(removeOp, options.Token, options.Timeout).ConfigureAwait(false);
        }

        public Task Unlock<T>(string id, TimeSpan? timeout = null, ulong cas = 0, CancellationToken token = default(CancellationToken))
        {
            var options = new UnlockOptions
            {
                Timeout = timeout,
                Cas = cas,
                Token = token
            };

            return Unlock<T>(id, options);
        }

        public Task Unlock<T>(string id, Action<UnlockOptions> optionsAction)
        {
            var options = new UnlockOptions();
            optionsAction(options);

            return Unlock<T>(id, options);
        }

        public async Task Unlock<T>(string id, UnlockOptions options)
        {
            var unlockOp = new Unlock
            {
                Key = id,
                Cid = Cid,
                Cas = options.Cas
            };

            await ExecuteOp(unlockOp, options.Token, options.Timeout).ConfigureAwait(false);
        }

        public Task Touch(string id, TimeSpan expiration, TimeSpan? timeout = null,
            DurabilityLevel durabilityLevel = DurabilityLevel.None, CancellationToken token = default(CancellationToken))
        {
            var options = new TouchOptions
            {
                Timeout = timeout,
                DurabilityLevel = durabilityLevel,
                Token = token
            };

            return Touch(id, expiration, options);
        }

        public Task Touch(string id, TimeSpan expiration, Action<TouchOptions> optionsAction)
        {
            var options = new TouchOptions();
            optionsAction(options);

            return Touch(id, expiration, options);
        }

        public async Task Touch(string id, TimeSpan expiration, TouchOptions options)
        {
            var touchOp = new Touch
            {
                Key = id,
                Cid = Cid,
                Expires = expiration.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500)
            };

            await ExecuteOp(touchOp, options.Token, options.Timeout).ConfigureAwait(false);
        }

        #region GetAndTouch

        public Task<IGetResult> GetAndTouch(string id, TimeSpan expiration, IEnumerable<string> projections = null,
            TimeSpan? timeout = null, DurabilityLevel durabilityLevel = DurabilityLevel.None,
            CancellationToken token = default(CancellationToken))
        {
            var options = new GetAndTouchOptions
            {
                Timeout = timeout,
                DurabilityLevel = durabilityLevel,
                Token = token
            };

            return GetAndTouch(id, expiration, options);
        }

        public Task<IGetResult> GetAndTouch(string id, TimeSpan expiration, Action<GetAndTouchOptions> optionsAction)
        {
            var options = new GetAndTouchOptions();
            optionsAction(options);

            return GetAndTouch(id, expiration, options);
        }

        public async Task<IGetResult> GetAndTouch(string id, TimeSpan expiration, GetAndTouchOptions options)
        {
            var getAndTouchOp = new GetT<byte[]>
            {
                Key = id,
                Cid = Cid,
                Expires = expiration.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500)
            };

            await ExecuteOp(getAndTouchOp, options.Token, options.Timeout);
            return new GetResult(getAndTouchOp.Data.ToArray(), _transcoder);
        }

        #endregion

        #region LookupIn

        private static void ConfigureLookupInOptions(LookupInOptions options, TimeSpan? timeout, CancellationToken token)
        {
            if (timeout.HasValue)
            {
                options.Timeout(timeout.Value);
            }

            if (token != CancellationToken.None)
            {
                options._Token = token;
            }
        }

        public Task<ILookupInResult> LookupIn(string id, Action<LookupInSpecBuilder> configureBuilder, TimeSpan? timeout = null,
            CancellationToken token = default(CancellationToken))
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            var options = new LookupInOptions();
            ConfigureLookupInOptions(options, timeout, token);

            return LookupIn(id, builder.Specs, options);
        }

        public Task<ILookupInResult> LookupIn(string id, Action<LookupInSpecBuilder> configureBuilder, Action<LookupInOptions> configureOptions)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            var options = new LookupInOptions();
            configureOptions(options);

            return LookupIn(id, builder.Specs, options);
        }

        public Task<ILookupInResult> LookupIn(string id, Action<LookupInSpecBuilder> configureBuilder, LookupInOptions options)
        {
            var lookupInSpec = new LookupInSpecBuilder();
            configureBuilder(lookupInSpec);

            return LookupIn(id, lookupInSpec.Specs, options);
        }

        public Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs, TimeSpan? timeout = null,
            CancellationToken token = default(CancellationToken))
        {
            var options = new LookupInOptions();
            ConfigureLookupInOptions(options, timeout, token);

            return LookupIn(id, specs, options);
        }

        public Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs, Action<LookupInOptions> configureOptions)
        {
            var options = new LookupInOptions();
            configureOptions(options);

            return LookupIn(id, specs, options);
        }

        public async Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs, LookupInOptions options)
        {
            var lookup = await ExecuteLookupIn(id, specs, options);
            return new LookupInResult(lookup.Data.ToArray(), lookup.Cas, null);
        }

        private async Task<MultiLookup<byte[]>> ExecuteLookupIn(string id, IEnumerable<OperationSpec> specs, LookupInOptions options)
        {
            // convert new style specs into old style builder
            var builder = new LookupInBuilder<byte[]>(null, null, id, specs);

            var lookup = new MultiLookup<byte[]>
            {
                Key = id,
                Builder = builder,
                Cid = Cid
            };

            await ExecuteOp(lookup, options._Token, options._Timeout);
            return lookup;
        }

        #endregion

        #region GetAndLock

        public Task<IGetResult> GetAndLock(string id, TimeSpan expiration, TimeSpan? timeout = null,
            CancellationToken token = default(CancellationToken))
        {
            var options = new GetAndLockOptions
            {
                Timeout = timeout,
                Token = token
            };

            return GetAndLock(id, expiration, options);
        }

        public Task<IGetResult> GetAndLock(string id, TimeSpan expiration, Action<GetAndLockOptions> optionsAction)
        {
            var options = new GetAndLockOptions();
            optionsAction(options);

            return GetAndLock(id, expiration, options);
        }

        public async Task<IGetResult> GetAndLock(string id, TimeSpan expiration, GetAndLockOptions options)
        {
            var getAndLockOp = new GetL<byte[]>
            {
                Key = id,
                Cid = Cid,
                Expiration = expiration.ToTtl()
            };

            await ExecuteOp(getAndLockOp, options.Token, options.Timeout);
            return new GetResult(getAndLockOp.Data.ToArray(), _transcoder);
        }

        #endregion

        #region MutateIn

        private static void ConfigureMutateInOptions(MutateInOptions options, TimeSpan? timeout, TimeSpan? expiration,
            ulong cas, bool createDocument, DurabilityLevel durabilityLevel, CancellationToken token)
        {
            if (timeout.HasValue)
            {
                options.Timeout(timeout.Value);
            }

            if (expiration.HasValue)
            {
                options.Expiration(expiration.Value);
            }

            if (cas > 0)
            {
                options.Cas(cas);
            }

            var flags = SubdocDocFlags.None;
            if (createDocument)
            {
                flags ^= SubdocDocFlags.UpsertDocument;
            }

            if (durabilityLevel != DurabilityLevel.None)
            {
                options._DurabilityLevel = durabilityLevel;
            }

            if (token != CancellationToken.None)
            {
                options._Token = token;
            }

            options.Flags(flags);
        }

        public Task<IMutationResult> MutateIn(string id, Action<MutateInSpecBuilder> configureBuilder, TimeSpan? timeout = null, TimeSpan? expiration = null, ulong cas = 0, bool createDocument = false,
            DurabilityLevel durabilityLevel = DurabilityLevel.None, CancellationToken token = default(CancellationToken))
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            var options = new MutateInOptions();
            ConfigureMutateInOptions(options, timeout, expiration, cas, createDocument, durabilityLevel, token);

            return MutateIn(id, builder.Specs, options);
        }

        public Task<IMutationResult> MutateIn(string id, Action<MutateInSpecBuilder> configureBuilder, Action<MutateInOptions> configureOptions)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            var options = new MutateInOptions();
            configureOptions(options);
            
            return MutateIn(id, builder.Specs, options);
        }

        public Task<IMutationResult> MutateIn(string id, Action<MutateInSpecBuilder> configureBuilder, MutateInOptions options)
        {
            var mutateInSpec = new MutateInSpecBuilder();
            configureBuilder(mutateInSpec);

            return MutateIn(id, mutateInSpec.Specs, options);
        }

        public Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs, TimeSpan? timeout = null, TimeSpan? expiration = null, ulong cas = 0, bool createDocument = false,
            DurabilityLevel durabilityLevel = DurabilityLevel.None, CancellationToken token = default(CancellationToken))
        {
            var options = new MutateInOptions();
            ConfigureMutateInOptions(options, timeout, expiration, cas, createDocument, durabilityLevel, token);

            return MutateIn(id, specs, options);
        }

        public Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs, Action<MutateInOptions> configureOptions)
        {
            var options = new MutateInOptions();
            configureOptions(options);

            return MutateIn(id, specs, options);
        }

        public async Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs, MutateInOptions options)
        {
            // convert new style specs into old style builder
            var builder = new MutateInBuilder<byte[]>(null, null, id, specs);

            var mutation = new MultiMutation<byte[]>
            {
                Key = id,
                Builder = builder,
                Cid = Cid,
                DurabilityLevel = options._DurabilityLevel
            };

            await ExecuteOp(mutation, options._Token, options._Timeout);
            return new MutationResult(mutation.Cas, null, mutation.MutationToken);
        }

        #endregion
    }
}
