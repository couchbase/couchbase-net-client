using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;

namespace Couchbase.KeyValue
{
    #region GetOptions

    public class GetOptions
    {
        public IRetryStrategy RetryStrategy { get; set; } = new BestEffortRetryStrategy();

        public bool IncludeExpiry { get; set; }

        public List<string> ProjectList { get; set; } = new List<string>();

        public TimeSpan? Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public ITypeTranscoder Transcoder { get; set; }

        public GetOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public GetOptions WithExpiry()
        {
            IncludeExpiry = true;
            return this;
        }

        public GetOptions WithProjection(params string[] fields)
        {
            ProjectList.AddRange(fields);
            return this;
        }

        public GetOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public GetOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region GetAnyReplicaOptions

    public class GetAllReplicasOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public ITypeTranscoder Transcoder { get; set; }

        public GetAllReplicasOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public  GetAllReplicasOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllReplicasOptions Default => new GetAllReplicasOptions();
    }

    #endregion

    #region GetAllReplicaOptions

    public class GetAnyReplicaOptions
    {
        public ITypeTranscoder Transcoder { get; set; }

        public GetAnyReplicaOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public CancellationToken CancellationToken { get; set; }

        public GetAnyReplicaOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAnyReplicaOptions Default => new GetAnyReplicaOptions();
    }

    #endregion

    #region Exists Options

    public class ExistsOptions
    {
        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public ExistsOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public ExistsOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Upsert Options

    public class UpsertOptions
    {
        public TimeSpan Expiry { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }
        public ITypeTranscoder Transcoder { get; set; }

        public UpsertOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public UpsertOptions WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
            return this;
        }

        public UpsertOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public UpsertOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public UpsertOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public UpsertOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Insert Options

    public class InsertOptions
    {
        public TimeSpan Expiry { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public ITypeTranscoder Transcoder { get; set; }

        public InsertOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public InsertOptions WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
            return this;
        }

        public InsertOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public InsertOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public InsertOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public InsertOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Replace Options

    public class ReplaceOptions
    {
        public TimeSpan Expiry { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public ITypeTranscoder Transcoder { get; set; }

        public ReplaceOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public ReplaceOptions WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
            return this;
        }

        public ReplaceOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public ReplaceOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public ReplaceOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public ReplaceOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public ReplaceOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Remove Options

    public class RemoveOptions
    {
        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public RemoveOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public RemoveOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public RemoveOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public RemoveOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public RemoveOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Unlock Options

    public class UnlockOptions
    {

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public UnlockOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public UnlockOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Touch Options

    public class TouchOptions
    {

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public TouchOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public TouchOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Increment Options

    public class IncrementOptions
    {
        public ulong Initial { get; set; } = 1;

        public ulong Delta { get; set; } = 1;

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public IncrementOptions WithInitial(ulong initial)
        {
            Initial = initial;
            return this;
        }

        public IncrementOptions WithDelta(ulong delta)
        {
            Delta = delta;
            return this;
        }

        public IncrementOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public IncrementOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public IncrementOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public IncrementOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public IncrementOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Decrement options

    public class DecrementOptions
    {
        public ulong Initial { get; set; } = 1;

        public ulong Delta { get; set; } = 1;

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public DecrementOptions WithInitial(ulong initial)
        {
            Initial = initial;
            return this;
        }

        public DecrementOptions WithDelta(ulong delta)
        {
            Delta = delta;
            return this;
        }

        public DecrementOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public DecrementOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public DecrementOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public DecrementOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public DecrementOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Append Options

    public class AppendOptions
    {
        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public AppendOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public AppendOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public AppendOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public AppendOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public AppendOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region Prepend Options

    public class PrependOptions
    {
        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public PrependOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public PrependOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public PrependOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public PrependOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public PrependOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region GetAndLock Options

    public class GetAndLockOptions
    {
        public TimeSpan? Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public ITypeTranscoder Transcoder { get; set; }

        public GetAndLockOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public GetAndLockOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public GetAndLockOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region GetAndTouch Options

    public class GetAndTouchOptions
    {

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public ITypeTranscoder Transcoder { get; set; }

        public GetAndTouchOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder;
            return this;
        }

        public GetAndTouchOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public GetAndTouchOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region LookupInOptions

    public class LookupInOptions
    {
        public TimeSpan Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public bool Expiry { get; set; }

        public ITypeSerializer Serializer { get; set; }

        public LookupInOptions WithSerializer(ITypeSerializer serializer)
        {
            Serializer = serializer;
            return this;
        }

        public LookupInOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public LookupInOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }

        public LookupInOptions WithExpiry(bool expiry)
        {
            Expiry = expiry;
            return this;
        }
    }

    #endregion

    #region MutateInOptions

    public class MutateInOptions
    {
        public TimeSpan expiry { get; set; }

        public StoreSemantics StoreSemantics { get; set; }

        public ulong Cas { get; set; }

        public Tuple<PersistTo, ReplicateTo> Durabilty { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public ITypeSerializer Serializer { get; set; }

        public MutateInOptions WithStoreSemantics(StoreSemantics storeSemantics)
        {
            StoreSemantics |= storeSemantics;
            return this;
        }

        public MutateInOptions WithSerializer(ITypeSerializer serializer)
        {
            Serializer = serializer;
            return this;
        }

        public MutateInOptions WithExpiry(TimeSpan expiry)
        {
            this.expiry = expiry;
            return this;
        }

        public MutateInOptions WithExpiry(int days = 0, int hours = 0, int minutes = 0, int seconds = 0, int milliseconds=0)
        {
            return WithExpiry(new TimeSpan(days, hours, minutes, seconds, milliseconds));
        }

        public MutateInOptions WithCreateDoc(bool createDoc)
        {
            if (createDoc)
            {
                StoreSemantics |= StoreSemantics.Insert;
            }
            return this;
        }

        public MutateInOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public MutateInOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Durabilty = new Tuple<PersistTo, ReplicateTo>(persistTo, replicateTo);
            return this;
        }

        public MutateInOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public MutateInOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public MutateInOptions WithTimeout(int minutes = 0, int seconds = 0, int milliseconds=0)
        {
            return WithTimeout(new TimeSpan(0, 0, minutes, seconds, milliseconds));
        }

        public MutateInOptions WithToken(CancellationToken token)
        {
            Token = token;
            return this;
        }
    }

    #endregion

    #region MutateIn Options

    public abstract class MutateInXattrOperation
    {
        public bool XAttr { get; set; }

        public MutateInXattrOperation WithXAttr()
        {
            XAttr = true;
            return this;
        }
    }

    public abstract class MutateInOperationOptions :  MutateInXattrOperation
    {
        public bool CreatePath { get; set; }

        public MutateInOperationOptions WithCreatePath()
        {
            CreatePath = true;
            return this;
        }
    }

    public sealed class MutateInInsertOptions : MutateInOperationOptions {}

    public sealed class MutateInUpsertOptions : MutateInOperationOptions {}

    public sealed class MutateInReplaceOptions : MutateInXattrOperation {}

    public sealed class MutateInRemoveOptions : MutateInXattrOperation {}

    public sealed class MutateInArrayAppendOptions : MutateInOperationOptions {}

    public sealed class MutateInArrayPrependOptions :MutateInOperationOptions {}

    public sealed class MutateInArrayInsertOptions : MutateInOperationOptions {}

    public sealed class MutateInArrayAddUniqueOptions : MutateInOperationOptions {}

    public sealed class MutateInIncrementOptions : MutateInOperationOptions {}

    public sealed class MutateInDecrementOptions : MutateInOperationOptions {}

    #endregion
}
