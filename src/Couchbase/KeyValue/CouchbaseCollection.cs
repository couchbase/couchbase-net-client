using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.KeyValue
{
    public class CouchbaseCollection : ICollection, IBinaryCollection
    {
        internal const string DefaultCollectionName = "_default";
        private static readonly ILogger Log = LogManager.CreateLogger<CouchbaseCollection>();
        private readonly BucketBase _bucket;
        private static readonly TimeSpan DefaultTimeout = new TimeSpan(0,0,0,0,2500);//temp
        private readonly ITypeTranscoder _transcoder = new DefaultTranscoder(new DefaultConverter());
        private readonly ClusterContext _context;

        internal CouchbaseCollection(BucketBase bucket, ClusterContext context, uint? cid, string name)
        {
            Cid = cid;
            Name = name;
            _bucket = bucket;
            _context = context;
        }

        public uint? Cid { get; }

        public string Name { get; }

        public IBinaryCollection Binary => this;

        #region Get

        public async Task<IGetResult> GetAsync(string id, GetOptions options = null)
        {
            options = options ?? new GetOptions();
            var specs = new List<OperationSpec>();
            if (options.IncludeExpiry)
            {
                specs.Add(new OperationSpec
                {
                    OpCode = OpCode.SubGet,
                    Path = VirtualXttrs.DocExpiryTime,
                    PathFlags = SubdocPathFlags.Xattr
                });
            }
            if (!options.Timeout.HasValue)
            {
                options.Timeout = DefaultTimeout;
            }

            var projectList = options.ProjectList;
            if (projectList.Any())
            {
                //we have succeeded the max #fields returnable by sub-doc so fetch the whole doc
                if (projectList.Count + specs.Count > 16)
                {
                    specs.Add(new OperationSpec
                    {
                        Path = "",
                        OpCode = OpCode.Get,
                        DocFlags = SubdocDocFlags.None
                    });
                }
                else
                {
                    //Add the projections for fetching
                    projectList.ForEach(path=>specs.Add(new OperationSpec
                    {
                        OpCode = OpCode.SubGet,
                        Path = path
                    }));
                }
            }
            else
            {
                //Project list is empty so fetch the whole doc
                specs.Add(new OperationSpec
                {
                    Path = "",
                    OpCode = OpCode.Get,
                    DocFlags = SubdocDocFlags.None
                });
            }

            var lookupOp = await ExecuteLookupIn(id,
                    specs, new LookupInOptions().WithTimeout(options.Timeout.Value))
                .ConfigureAwait(false);

            var transcoder = options.Transcoder ?? _transcoder;
            return new GetResult(lookupOp.ExtractData(), transcoder, specs, projectList)
            {
                Id = lookupOp.Key,
                Cas = lookupOp.Cas,
                OpCode = lookupOp.OpCode,
                Flags = lookupOp.Flags,
                Header = lookupOp.Header
            };
        }

        #endregion

        #region Exists

        public async Task<IExistsResult> ExistsAsync(string id, ExistsOptions options = null)
        {
            options = options ?? new ExistsOptions();
            using (var existsOp = new Observe
            {
                Key = id,
                Cid = Cid,
                Transcoder = _transcoder
            })
            {
                try
                {
                    await _bucket.SendAsync(existsOp, options.Token, options.Timeout);
                    var value = existsOp.GetValue();
                    return new ExistsResult
                    {
                        Exists = existsOp.Success && value.KeyState != KeyState.NotFound &&
                                 value.KeyState != KeyState.LogicalDeleted,
                        Cas = value.Cas,
                        Expiry = TimeSpan.FromMilliseconds(existsOp.Expires)
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
        }

        #endregion

        #region Upsert

        public async Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions options = null)
        {
            options = options ?? new UpsertOptions();
            var transcoder = options.Transcoder ?? _transcoder;
            using (var upsertOp = new Set<T>
            {
                Key = id,
                Content = content,
                Cid = Cid,
                Expires = options.Expiry.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Transcoder = transcoder
            })
            {
                await _bucket.SendAsync(upsertOp, options.Token, options.Timeout).ConfigureAwait(false);
                return new MutationResult(upsertOp.Cas, null, upsertOp.MutationToken);
            }
        }

        #endregion

        #region Insert

        public async Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions options = null)
        {
            options = options ?? new InsertOptions();
            var transcoder = options.Transcoder ?? _transcoder;
            using (var insertOp = new Add<T>
            {
                Key = id,
                Content = content,
                Cid = Cid,
                Expires = options.Expiry.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Transcoder = transcoder
            })
            {
                await _bucket.SendAsync(insertOp, options.Token, options.Timeout).ConfigureAwait(false);
                return new MutationResult(insertOp.Cas, null, insertOp.MutationToken);
            }
        }

        #endregion

        #region Replace

        public async Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions options = null)
        {
            options = options ?? new ReplaceOptions();
            var transcoder = options.Transcoder ?? _transcoder;
            using (var replaceOp = new Replace<T>
            {
                Key = id,
                Content = content,
                Cas = options.Cas,
                Cid = Cid,
                Expires = options.Expiry.ToTtl(),
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Transcoder = transcoder
            })
            {
                await _bucket.SendAsync(replaceOp, options.Token, options.Timeout).ConfigureAwait(false);
                return new MutationResult(replaceOp.Cas, null, replaceOp.MutationToken);
            }
        }

        #endregion

        #region Remove

        public async Task RemoveAsync(string id, RemoveOptions options = null)
        {
            options = options ?? new RemoveOptions();
            using (var removeOp = new Delete
            {
                Key = id,
                Cas = options.Cas,
                Cid = Cid,
                DurabilityLevel = options.DurabilityLevel,
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Transcoder = _transcoder
            })
            {
                await _bucket.SendAsync(removeOp, options.Token, options.Timeout).ConfigureAwait(false);
            }
        }

        #endregion

        #region Unlock

        public async Task UnlockAsync<T>(string id, ulong cas, UnlockOptions options = null)
        {
            options = options ?? new UnlockOptions();
            using (var unlockOp = new Unlock
            {
                Key = id,
                Cid = Cid,
                Cas = cas,
                Transcoder = _transcoder
            })
            {
                await _bucket.SendAsync(unlockOp, options.Token, options.Timeout).ConfigureAwait(false);
            }
        }

        #endregion

        #region Touch

        public async Task TouchAsync(string id, TimeSpan expiry, TouchOptions options = null)
        {
            options = options ?? new TouchOptions();
            using (var touchOp = new Touch
            {
                Key = id,
                Cid = Cid,
                Expires = expiry.ToTtl(),
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Transcoder = _transcoder
            })
            {
                await _bucket.SendAsync(touchOp, options.Token, options.Timeout).ConfigureAwait(false);
            }
        }

        #endregion

        #region GetAndTouch

        public async Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions options = null)
        {
            options = options ?? new GetAndTouchOptions();
            var transcoder = options.Transcoder ?? _transcoder;
            using (var getAndTouchOp = new GetT<byte[]>
            {
                Key = id,
                Cid = Cid,
                Expires = expiry.ToTtl(),
                DurabilityTimeout = TimeSpan.FromMilliseconds(1500),
                Transcoder = transcoder
            })
            {
                await _bucket.SendAsync(getAndTouchOp, options.Token, options.Timeout);
                return new GetResult(getAndTouchOp.ExtractData(), transcoder)
                {
                    Id = getAndTouchOp.Key,
                    Cas = getAndTouchOp.Cas,
                    Flags = getAndTouchOp.Flags,
                    Header = getAndTouchOp.Header,
                    OpCode = getAndTouchOp.OpCode
                };
            }
        }

        #endregion

        #region GetAndLock

        public async Task<IGetResult> GetAndLockAsync(string id, TimeSpan lockTime, GetAndLockOptions options = null)
        {
            options = options ?? new GetAndLockOptions();
            var transcoder = options.Transcoder ?? _transcoder;
            using (var getAndLockOp = new GetL<byte[]>
            {
                Key = id,
                Cid = Cid,
                Expiry = lockTime.ToTtl(),
                Transcoder = transcoder
            })
            {
                await _bucket.SendAsync(getAndLockOp, options.Token, options.Timeout);
                return new GetResult(getAndLockOp.ExtractData(), transcoder)
                {
                    Id = getAndLockOp.Key,
                    Cas = getAndLockOp.Cas,
                    Flags = getAndLockOp.Flags,
                    Header = getAndLockOp.Header,
                    OpCode = getAndLockOp.OpCode
                };
            }
        }

        #endregion

        #region LookupIn

        public async Task<ILookupInResult> LookupInAsync(string id, IEnumerable<OperationSpec> specs, LookupInOptions options = null)
        {
            options = options ?? new LookupInOptions();
            using (var lookup = await ExecuteLookupIn(id, specs, options))
            {
                return new LookupInResult(lookup.ExtractData(), lookup.Cas, null);
            }
        }

        private async Task<MultiLookup<byte[]>> ExecuteLookupIn(string id, IEnumerable<OperationSpec> specs, LookupInOptions options)
        {
            // convert new style specs into old style builder
            var builder = new LookupInBuilder<byte[]>(null, null, id, specs);

            //add the virtual xttar attribute to get the doc expiration time
            if (options.Expiry)
            {
                builder.Get(VirtualXttrs.DocExpiryTime, SubdocPathFlags.Xattr);
            }

            var lookup = new MultiLookup<byte[]>
            {
                Key = id,
                Builder = builder,
                Cid = Cid,
                Transcoder = _transcoder
            };

            await _bucket.RetryAsync(lookup, options.Token, options.Timeout);
            return lookup;
        }

        #endregion

        #region MutateIn

        public async Task<IMutationResult> MutateInAsync(string id, IEnumerable<OperationSpec> specs, MutateInOptions options = null)
        {
            options = options ?? new MutateInOptions();
            // convert new style specs into old style builder
            var builder = new MutateInBuilder<byte[]>(null, null, id, specs);

            //resolve StoreSemantics to SubdocDocFlags
            var docFlags = SubdocDocFlags.None;
            switch (options.StoreSemantics)
            {
                case StoreSemantics.Replace:
                    break;
                case StoreSemantics.Upsert:
                    docFlags |= SubdocDocFlags.UpsertDocument;
                    break;
                case StoreSemantics.Insert:
                    docFlags |= SubdocDocFlags.InsertDocument;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            using (var mutation = new MultiMutation<byte[]>
            {
                Key = id,
                Builder = builder,
                Cid = Cid,
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = _transcoder,
                DocFlags = docFlags
            })
            {
                await _bucket.SendAsync(mutation, options.Token, options.Timeout);
                return new MutationResult(mutation.Cas, null, mutation.MutationToken);
            }
        }

        #endregion

        #region Append

        public async Task<IMutationResult> AppendAsync(string id, byte[] value, AppendOptions options = null)
        {
            options = options ?? new AppendOptions();
            using (var op = new Append<byte[]>
            {
                Cid = Cid,
                Key = id,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = _transcoder
            })
            {
                await _bucket.SendAsync(op, options.Token, options.Timeout);
                return new MutationResult(op.Cas, null, op.MutationToken);
            }
        }

        #endregion

        #region Prepend

        public async Task<IMutationResult> PrependAsync(string id, byte[] value, PrependOptions options = null)
        {
            options = options ?? new PrependOptions();
            using (var op = new Prepend<byte[]>
            {
                Cid = Cid,
                Key = id,
                Content = value,
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = _transcoder
            })
            {
                await _bucket.SendAsync(op, options.Token, options.Timeout);
                return new MutationResult(op.Cas, null, op.MutationToken);
            }
        }

        #endregion

        #region Increment

        public async Task<ICounterResult> IncrementAsync(string id, IncrementOptions options = null)
        {
            options = options ?? new IncrementOptions();
            using (var op = new Increment
            {
                Cid = Cid,
                Key = id,
                Delta = options.Delta,
                Initial = options.Initial,
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = _transcoder
            })
            {
                await _bucket.SendAsync(op, options.Token, options.Timeout);
                return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
            }
        }

        #endregion

        #region Decrement

        public async Task<ICounterResult> DecrementAsync(string id, DecrementOptions options = null)
        {
            options = options ?? new DecrementOptions();
            using (var op = new Decrement
            {
                Cid = Cid,
                Key = id,
                Delta = options.Delta,
                Initial = options.Initial,
                DurabilityLevel = options.DurabilityLevel,
                Transcoder = _transcoder
            })
            {
                await _bucket.SendAsync(op, options.Token, options.Timeout);
                return new CounterResult(op.GetValue(), op.Cas, null, op.MutationToken);
            }
        }

        #endregion

        #region GetAnyReplica / GetAllReplicas

        public async Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions options = null)
        {
            options = options ?? new GetAnyReplicaOptions();
            var vBucket = (VBucket) _bucket.KeyMapper.MapKey(id);
            if (!vBucket.HasReplicas)
            {
                Log.LogWarning($"Call to GetAnyReplica for key [{id}] but none are configured. Only the active document will be retrieved.");
            }

            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1);

            var transcoder = options.Transcoder ?? _transcoder;

            // get primary
            tasks.Add(GetPrimary(id, options.CancellationToken, transcoder));

            // get replicas
            tasks.AddRange(vBucket.Replicas.Select(index => GetReplica(id, index, options.CancellationToken, transcoder)));

            return await Task.WhenAny(tasks).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions options = null)
        {
            options = options ?? new GetAllReplicasOptions();
            var vBucket = (VBucket) _bucket.KeyMapper.MapKey(id);
            if (!vBucket.HasReplicas)
            {
                Log.LogWarning($"Call to GetAllReplicas for key [{id}] but none are configured. Only the active document will be retrieved.");
            }

            var tasks = new List<Task<IGetReplicaResult>>(vBucket.Replicas.Length + 1);

            var transcoder = options.Transcoder ?? _transcoder;

            // get primary
            tasks.Add(GetPrimary(id, options.CancellationToken, transcoder));

            // get replicas
            tasks.AddRange(vBucket.Replicas.Select(index => GetReplica(id, index, options.CancellationToken, transcoder)));

            return tasks;
        }

        private async Task<IGetReplicaResult> GetPrimary(string id, CancellationToken cancellationToken, ITypeTranscoder transcoder)
        {
            using (var getOp = new Get<object>
            {
                Key = id,
                Cid = Cid,
                Transcoder = transcoder
            })
            {
                await _bucket.RetryAsync(getOp, cancellationToken).ConfigureAwait(false);
                return new GetReplicaResult(getOp.ExtractData(), transcoder)
                {
                    Id = getOp.Key,
                    Cas = getOp.Cas,
                    OpCode = getOp.OpCode,
                    Flags = getOp.Flags,
                    Header = getOp.Header,
                    IsActive = true
                };
            }
        }

        private async Task<IGetReplicaResult> GetReplica(string id, short index, CancellationToken cancellationToken, ITypeTranscoder transcoder)
        {
            using (var getOp = new ReplicaRead<object>
            {
                Key = id,
                Cid = Cid,
                VBucketId = index,
                Transcoder = transcoder
            })
            {
                await _bucket.RetryAsync(getOp, cancellationToken).ConfigureAwait(false);
                return new GetReplicaResult(getOp.ExtractData(), transcoder)
                {
                    Id = getOp.Key,
                    Cas = getOp.Cas,
                    OpCode = getOp.OpCode,
                    Flags = getOp.Flags,
                    Header = getOp.Header,
                    IsActive = false
                };
            }
        }

        #endregion
    }
}
