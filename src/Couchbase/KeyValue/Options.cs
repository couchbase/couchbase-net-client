using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.KeyValue
{
    #region GetOptions

    public class GetOptions
    {
        internal IRetryStrategy RetryStrategyValue { get; set; } = new BestEffortRetryStrategy();

        internal bool IncludeExpiryValue { get; set; }

        internal List<string> ProjectListValue { get; set; } = new List<string>();

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken TokenValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        public GetOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        public GetOptions Expiry()
        {
            IncludeExpiryValue = true;
            return this;
        }

        public GetOptions Projection(params string[] fields)
        {
            ProjectListValue.AddRange(fields);
            return this;
        }

        public GetOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public GetOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region GetAnyReplicaOptions

    public class GetAllReplicasOptions
    {
        internal CancellationToken TokenValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        public GetAllReplicasOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        public GetAllReplicasOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static GetAllReplicasOptions Default => new GetAllReplicasOptions();
    }

    #endregion

    #region GetAllReplicaOptions

    public class GetAnyReplicaOptions
    {
        internal ITypeTranscoder? TranscoderValue { get; set; }

        public GetAnyReplicaOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public GetAnyReplicaOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static GetAnyReplicaOptions Default => new GetAnyReplicaOptions();
    }

    #endregion

    #region Exists Options

    public class ExistsOptions
    {
        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public ExistsOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public ExistsOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Upsert Options

    public class UpsertOptions
    {
        internal TimeSpan ExpiryValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        public UpsertOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        public UpsertOptions Expiry(TimeSpan expiry)
        {
            ExpiryValue = expiry;
            return this;
        }

        public UpsertOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public UpsertOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public UpsertOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public UpsertOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Insert Options

    public class InsertOptions
    {
        internal TimeSpan ExpiryValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        public InsertOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        public InsertOptions Expiry(TimeSpan expiry)
        {
            ExpiryValue = expiry;
            return this;
        }

        public InsertOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public InsertOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public InsertOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public InsertOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Replace Options

    public class ReplaceOptions
    {
        internal TimeSpan ExpiryValue { get; set; }

        internal ulong CasValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        public ReplaceOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        public ReplaceOptions Expiry(TimeSpan expiry)
        {
            ExpiryValue = expiry;
            return this;
        }

        public ReplaceOptions Cas(ulong cas)
        {
            CasValue = cas;
            return this;
        }

        public ReplaceOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public ReplaceOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public ReplaceOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public ReplaceOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Remove Options

    public class RemoveOptions
    {
        internal ulong CasValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public RemoveOptions Cas(ulong cas)
        {
            CasValue = cas;
            return this;
        }

        public RemoveOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public RemoveOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public RemoveOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public RemoveOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Unlock Options

    public class UnlockOptions
    {

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public UnlockOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public UnlockOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Touch Options

    public class TouchOptions
    {

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public TouchOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public TouchOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Increment Options

    public class IncrementOptions
    {
        internal ulong InitialValue { get; set; } = 1;

        internal ulong DeltaValue { get; set; } = 1;

        internal ulong CasValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public IncrementOptions Initial(ulong initial)
        {
            InitialValue = initial;
            return this;
        }

        public IncrementOptions Delta(ulong delta)
        {
            DeltaValue = delta;
            return this;
        }

        public IncrementOptions Cas(ulong cas)
        {
            CasValue = cas;
            return this;
        }

        public IncrementOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public IncrementOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public IncrementOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public IncrementOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Decrement options

    public class DecrementOptions
    {
        internal ulong InitialValue { get; set; } = 1;

        internal ulong DeltaValue { get; set; } = 1;

        internal ulong CasValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public DecrementOptions Initial(ulong initial)
        {
            InitialValue = initial;
            return this;
        }

        public DecrementOptions Delta(ulong delta)
        {
            DeltaValue = delta;
            return this;
        }

        public DecrementOptions Cas(ulong cas)
        {
            CasValue = cas;
            return this;
        }

        public DecrementOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public DecrementOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public DecrementOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public DecrementOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Append Options

    public class AppendOptions
    {
        internal ulong CasValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public AppendOptions Cas(ulong cas)
        {
            CasValue = cas;
            return this;
        }

        public AppendOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public AppendOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public AppendOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public AppendOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Prepend Options

    public class PrependOptions
    {
        internal ulong CasValue { get; set; }

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        public PrependOptions Cas(ulong cas)
        {
            CasValue = cas;
            return this;
        }

        public PrependOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public PrependOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public PrependOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public PrependOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region GetAndLock Options

    public class GetAndLockOptions
    {
        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        public GetAndLockOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        public GetAndLockOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public GetAndLockOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region GetAndTouch Options

    public class GetAndTouchOptions
    {

        internal ReplicateTo ReplicateTo { get; set; }

        internal PersistTo PersistTo { get; set; }

        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        public GetAndTouchOptions Transcoder(ITypeTranscoder? transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        public GetAndTouchOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public GetAndTouchOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region LookupInOptions

    public class LookupInOptions
    {
        internal TimeSpan? TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        internal bool ExpiryValue { get; set; }

        internal ITypeSerializer? SerializerValue { get; set; }

        internal bool AccessDeletedValue { get; set; }

        public LookupInOptions Serializer(ITypeSerializer? serializer)
        {
            SerializerValue = serializer;
            return this;
        }

        public LookupInOptions Timeout(TimeSpan? timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public LookupInOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public LookupInOptions Expiry(bool expiry)
        {
            ExpiryValue = expiry;
            return this;
        }

        public LookupInOptions AccessDeleted(bool accessDeleted)
        {
            AccessDeletedValue = accessDeleted;
            return this;
        }
    }

    #endregion

    #region MutateInOptions

    public class MutateInOptions
    {
        internal TimeSpan ExpiryValue { get; set; }

        internal StoreSemantics StoreSemanticsValue { get; set; }

        internal ulong CasValue { get; set; }

        internal ValueTuple<PersistTo, ReplicateTo> DurabilityValue { get; set; }

        internal DurabilityLevel DurabilityLevel { get; set; }

        internal TimeSpan TimeoutValue { get; set; }

        internal CancellationToken? TokenValue { get; set; }

        internal ITypeSerializer? SerializerValue { get; set; }

        internal bool CreateAsDeletedValue { get; set; }

        public MutateInOptions StoreSemantics(StoreSemantics storeSemantics)
        {
            StoreSemanticsValue = storeSemantics;
            return this;
        }

        public MutateInOptions Serializer(ITypeSerializer? serializer)
        {
            SerializerValue = serializer;
            return this;
        }

        public MutateInOptions Expiry(TimeSpan expiry)
        {
            ExpiryValue = expiry;
            return this;
        }

        public MutateInOptions Cas(ulong cas)
        {
            CasValue = cas;
            return this;
        }

        public MutateInOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            DurabilityValue = new ValueTuple<PersistTo, ReplicateTo>(persistTo, replicateTo);
            return this;
        }

        public MutateInOptions Durability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public MutateInOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public MutateInOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public MutateInOptions CreateAsDeleted(bool createAsDeleted)
        {
            CreateAsDeletedValue = true;
            return this;
        }
    }

    #endregion

    #region MutateIn Options

    public abstract class MutateInXattrOperation
    {
        internal bool XAttrValue { get; set; }

        public MutateInXattrOperation XAttr()
        {
            XAttrValue = true;
            return this;
        }
    }

    public abstract class MutateInOperationOptions :  MutateInXattrOperation
    {
        internal bool CreatePathValue { get; set; }

        public MutateInOperationOptions CreatePath()
        {
            CreatePathValue = true;
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
