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
using Couchbase.Core.IO.Transcoders;

namespace Couchbase
{
    public class CouchbaseCollection : ICollection, IBinaryCollection
    {
        internal const string DefaultCollection = "_default";
        private readonly IBucketSender _bucket;
        private static readonly TimeSpan DefaultTimeout = new TimeSpan(0,0,0,0,2500);//temp
        private readonly ITypeTranscoder _transcoder = new DefaultTranscoder(new DefaultConverter());

        public CouchbaseCollection(IBucket bucket, uint? cid, string name)
        {
            Cid = cid;
            Name = name;
            _bucket = (IBucketSender)bucket;
        }

        public uint? Cid { get; }

        public string Name { get; }

        public IBinaryCollection Binary => this;

        #region ExecuteOp Helper

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
                    tcs.SetException(KeyValueException.Create(state.Status, errorMap : state.ErrorMap));
                }

                return tcs.Task;
            };

            CancellationTokenSource cts = null;
            try
            {
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
                }
            }
            catch (OperationCanceledException e)
            {
                if (!e.CancellationToken.IsCancellationRequested)
                {
                    //oddly IsCancellationRequested is false when timed out
                    throw new TimeoutException();
                }
            }
            finally
            {
                //clean up the token if we used a default token
                cts?.Dispose();
            }
        }

        #endregion

        #region Get

        public Task<IGetResult> Get(string id)
        {
            return Get(id, new GetOptions());
        }

        public Task<IGetResult> Get(string id, Action<GetOptions> configureOptions)
        {
            var options = new GetOptions();
            configureOptions?.Invoke(options);

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

                var lookupOp = await ExecuteLookupIn(id, specs, new LookupInOptions().WithTimeout(options.Timeout.Value));
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
            return new GetResult(getOp.Data.ToArray(), _transcoder)
            {
                Id = getOp.Key,
                Cas = getOp.Cas,
                OpCode = getOp.OpCode,
                Flags = getOp.Flags,
                Header = getOp.Header
            };
        }

        #endregion

        #region Exists

        public Task<IExistsResult> Exists(string id)
        {
            return Exists(id, new ExistsOptions());
        }

        public Task<IExistsResult> Exists(string id, Action<ExistsOptions> configureOptions)
        {
            var options = new ExistsOptions();
            configureOptions?.Invoke(options);

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

        #endregion

        #region Upsert

        public Task<IMutationResult> Upsert<T>(string id, T content)
        {
            return Upsert(id, content, new UpsertOptions());
        }

        public Task<IMutationResult> Upsert<T>(string id, T content, Action<UpsertOptions> configureOptions)
        {
            var options = new UpsertOptions();
            configureOptions(options);

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

        #endregion

        #region Insert

        public Task<IMutationResult> Insert<T>(string id, T content)
        {
            return Insert(id, content, new InsertOptions());
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

        #endregion

        #region Replace

        public Task<IMutationResult> Replace<T>(string id, T content)
        {
            return Replace(id, content, new ReplaceOptions());
        }

        public Task<IMutationResult> Replace<T>(string id, T content, Action<ReplaceOptions> configureOptions)
        {
            var options = new ReplaceOptions();
            configureOptions(options);

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

        #endregion

        #region Remove

        public Task Remove(string id)
        {
            return Remove(id, new RemoveOptions());
        }

        public Task Remove(string id, Action<RemoveOptions> configureOptions)
        {
            var options = new RemoveOptions();
            configureOptions(options);

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

        #endregion

        #region Unlock

        public Task Unlock<T>(string id)
        {
            return Unlock<T>(id, new UnlockOptions());
        }

        public Task Unlock<T>(string id, Action<UnlockOptions> configureOptions)
        {
            var options = new UnlockOptions();
            configureOptions(options);

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

        #endregion

        #region Touch

        public Task Touch(string id, TimeSpan expiration)
        {
            return Touch(id, expiration, new TouchOptions());
        }

        public Task Touch(string id, TimeSpan expiration, Action<TouchOptions> configureOptions)
        {
            var options = new TouchOptions();
            configureOptions(options);

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

        #endregion

        #region GetAndTouch

        public Task<IGetResult> GetAndTouch(string id, TimeSpan expiration)
        {
            return GetAndTouch(id, expiration, new GetAndTouchOptions());
        }

        public Task<IGetResult> GetAndTouch(string id, TimeSpan expiration, Action<GetAndTouchOptions> configureOptions)
        {
            var options = new GetAndTouchOptions();
            configureOptions(options);

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

        #region GetAndLock

        public Task<IGetResult> GetAndLock(string id, TimeSpan expiration)
        {
            return GetAndLock(id, expiration, new GetAndLockOptions());
        }

        public Task<IGetResult> GetAndLock(string id, TimeSpan expiration, Action<GetAndLockOptions> configureOptions)
        {
            var options = new GetAndLockOptions();
            configureOptions(options);

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

        #region LookupIn

        public Task<ILookupInResult> LookupIn(string id, Action<LookupInSpecBuilder> configureBuilder)
        {
            var builder = new LookupInSpecBuilder();
            configureBuilder(builder);

            return LookupIn(id, builder.Specs, new LookupInOptions());
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

        public Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs)
        {
            return LookupIn(id, specs, new LookupInOptions());
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

            await ExecuteOp(lookup, options.Token, options.Timeout);
            return lookup;
        }

        #endregion

        #region MutateIn

        public Task<IMutationResult> MutateIn(string id, Action<MutateInSpecBuilder> configureBuilder)
        {
            var builder = new MutateInSpecBuilder();
            configureBuilder(builder);

            return MutateIn(id, builder.Specs, new MutateInOptions());
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

        public Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs)
        {
            return MutateIn(id, specs, new MutateInOptions());
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
                DurabilityLevel = options.DurabilityLevel
            };

            await ExecuteOp(mutation, options.Token, options.Timeout);
            return new MutationResult(mutation.Cas, null, mutation.MutationToken);
        }

        #endregion

        #region Append

        public Task<IMutationResult> Append(string id, byte[] value)
        {
            return Append(id, value, new AppendOptions());
        }

        public Task<IMutationResult> Append(string id, byte[] value, Action<AppendOptions> configureOptions)
        {
            var options = new AppendOptions();
            configureOptions(options);

            return Append(id, value, options);
        }

        public async Task<IMutationResult> Append(string id, byte[] value, AppendOptions options)
        {
            var op = new Append<byte[]>
            {
                Cid = Cid,
                Key = id,
                Content = value,
                DurabilityLevel = options.DurabilityLevel
            };

            await ExecuteOp(op, options.Token, options.Timeout);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Prepend

        public Task<IMutationResult> Prepend(string id, byte[] value)
        {
            return Prepend(id, value, new PrependOptions());
        }

        public Task<IMutationResult> Prepend(string id, byte[] value, Action<PrependOptions> configureOptions)
        {
            var options = new PrependOptions();
            configureOptions(options);

            return Prepend(id, value, options);
        }

        public async Task<IMutationResult> Prepend(string id, byte[] value, PrependOptions options)
        {
            var op = new Prepend<byte[]>
            {
                Cid = Cid,
                Key = id,
                Content = value,
                DurabilityLevel = options.DurabilityLevel
            };

            await ExecuteOp(op, options.Token, options.Timeout);
            return new MutationResult(op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Increment

        public Task<ICounterResult> Increment(string id)
        {
            return Increment(id, new IncrementOptions());
        }

        public Task<ICounterResult> Increment(string id, Action<IncrementOptions> configureOptions)
        {
            var options = new IncrementOptions();
            configureOptions(options);

            return Increment(id, options);
        }

        public async Task<ICounterResult> Increment(string id, IncrementOptions options)
        {
            var op = new Increment
            {
                Cid = Cid,
                Key = id,
                Delta = options.Delta,
                Initial = options.Initial,
                DurabilityLevel = options.DurabilityLevel
            };

            await ExecuteOp(op, options.Token, options.Timeout);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion

        #region Decrement

        public Task<ICounterResult> Decrement(string id)
        {
            return Decrement(id, new DecrementOptions());
        }

        public Task<ICounterResult> Decrement(string id, Action<DecrementOptions> configureOptions)
        {
            var options = new DecrementOptions();
            configureOptions(options);

            return Decrement(id, options);
        }

        public async Task<ICounterResult> Decrement(string id, DecrementOptions options)
        {
            var op = new Decrement
            {
                Cid = Cid,
                Key = id,
                Delta = options.Delta,
                Initial = options.Initial,
                DurabilityLevel = options.DurabilityLevel
            };

            await ExecuteOp(op, options.Token, options.Timeout);
            return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
        }

        #endregion
    }
}
