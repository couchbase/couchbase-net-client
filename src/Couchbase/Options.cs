using System;
using System.Collections.Generic;
using System.Threading;

namespace Couchbase
{
    #region GetOptions

    public class GetOptions
    {
        public bool IncludeExpiration { get; set; }

        public bool CreatePath { get; set; }

        public List<string> ProjectList { get; set; }

        public TimeSpan? Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public GetOptions WithExpiration()
        {
            IncludeExpiration = true;
            return this;
        }

        public GetOptions WithCreatePath(bool createPath)
        {
            CreatePath = createPath;
            return this;
        }

        public GetOptions WithProjection(params string[] fields)
        {
            if (ProjectList == null)
            {
                ProjectList = new List<string>();
            }

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
        public TimeSpan Expiration { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public UpsertOptions WithExpiration(TimeSpan expiration)
        {
            Expiration = expiration;
            return this;
        }

        public UpsertOptions WithCas(ulong cas)
        {
            Cas = cas;
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
        public TimeSpan Expiration { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public InsertOptions WithExpiration(TimeSpan expiration)
        {
            Expiration = expiration;
            return this;
        }

        public InsertOptions WithCas(ulong cas)
        {
            Cas = cas;
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
        public TimeSpan Expiration { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public ReplaceOptions WithExpiration(TimeSpan expiration)
        {
            Expiration = expiration;
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
        public ulong Cas { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }
        
        public UnlockOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

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
        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }
        
        public TouchOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public TouchOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public TouchOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
            return this;
        }

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
        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }
        
        public GetAndTouchOptions WithCas(ulong cas)
        {
            Cas = cas;
            return this;
        }

        public GetAndTouchOptions WithDurability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public GetAndTouchOptions WithDurability(DurabilityLevel durabilityLevel)
        {
            DurabilityLevel = durabilityLevel;
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
    }

    #endregion

    #region MutateInOptions

    public class MutateInOptions
    {
        public TimeSpan Expiration { get; set; }

        public SubdocDocFlags Flags { get; set; }

        public ulong Cas { get; set; }

        public Tuple<PersistTo, ReplicateTo> Durabilty { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public MutateInOptions WithExpiration(TimeSpan expiration)
        {
            Expiration = expiration;
            return this;
        }

        public MutateInOptions WithExpiration(int days = 0, int hours = 0, int minutes = 0, int seconds = 0, int milliseconds=0)
        {
            return WithExpiration(new TimeSpan(days, hours, minutes, seconds, milliseconds));
        }

        public MutateInOptions WithCreateDoc(bool createDoc)
        {
            if (createDoc)
            {
                Flags = Flags | SubdocDocFlags.InsertDocument;
            }
            return this;
        }

        public MutateInOptions WithFlags(SubdocDocFlags flags)
        {
            Flags = flags;
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
}
