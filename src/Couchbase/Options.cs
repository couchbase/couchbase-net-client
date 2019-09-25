using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Services.KeyValue;

namespace Couchbase
{
    #region GetOptions

    public class GetOptions
    {
        public bool IncludeExpiry { get; set; }

        public bool CreatePath { get; set; }

        public List<string> ProjectList { get; set; } = new List<string>();

        public TimeSpan? Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public GetOptions WithExpiry()
        {
            IncludeExpiry = true;
            return this;
        }

        public GetOptions WithCreatePath(bool createPath)
        {
            CreatePath = createPath;
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

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public UpsertOptions WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
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
        public TimeSpan Expiry { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

        public InsertOptions WithExpiry(TimeSpan expiry)
        {
            Expiry = expiry;
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
        public TimeSpan Expiry { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan? Timeout { get;set; }

        public CancellationToken Token { get; set; }

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

        public bool Expiry { get; set; }

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

        public SubdocDocFlags Flags { get; set; }

        public ulong Cas { get; set; }

        public Tuple<PersistTo, ReplicateTo> Durabilty { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public TimeSpan Timeout { get; set; }

        public CancellationToken Token { get; set; }

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

    #region ViewOptions

    /// <summary>
    /// Allow the results from a stale view to be used
    /// </summary>
    public enum StaleState
    {
        None,

        //Force a view update before returning data
        False,
        //Allow stale views
        Ok,
        //Allow stale view, update view after it has been accessed
        UpdateAfter
    }

    public class ViewOptions
    {
        public StaleState StaleState { get; set; } = StaleState.None;
        public int? Skip { get; set; }
        public int? Limit { get; set; }
        public object StartKey { get; set; }
        public object StartKeyDocId { get; set; }
        public object EndKey { get; set; }
        public object EndKeyDocId { get; set; }
        public bool? InclusiveEnd { get; set; }
        public bool? Group { get; set; }
        public int? GroupLevel { get; set; }
        public object Key { get; set; }
        public object[] Keys { get; set; }
        public bool? Descending { get; set; }
        public bool? Reduce { get; set; }
        public bool? Development { get; set; }
        public bool? FullSet { get; set; }
        public bool? ContinueOnError { get; set; }
        public int? ConnectionTimeout { get; set; }

        public ViewOptions WithStaleState(StaleState staleState)
        {
            StaleState = staleState;
            return this;
        }

        public ViewOptions WithSkip(int skip)
        {
            Skip = skip;
            return this;
        }

        public ViewOptions WithLimit(int limit)
        {
            Limit = limit;
            return this;
        }

        public ViewOptions WithStartKey(object startKey)
        {
            StartKey = startKey;
            return this;
        }

        public ViewOptions WithStartKeyDocId(object startKyDocId)
        {
            StartKeyDocId = startKyDocId;
            return this;
        }

        public ViewOptions WithEndKey(object endKey)
        {
            EndKey = endKey;
            return this;
        }

        public ViewOptions WithEndKeyDocId(object endKeyDocId)
        {
            EndKeyDocId = endKeyDocId;
            return this;
        }

        public ViewOptions WithInclusiveEnd(bool inclusiveEnd)
        {
            InclusiveEnd = inclusiveEnd;
            return this;
        }

        public ViewOptions WithKey(object key)
        {
            Key = key;
            return this;
        }

        public ViewOptions WithKeys(params object[] keys)
        {
            Keys = keys;
            return this;
        }

        public ViewOptions WithAscending(bool ascending)
        {
            Descending = !ascending;
            return this;
        }

        public ViewOptions WithGroup(bool group)
        {
            Group = group;
            return this;
        }

        public ViewOptions WithGroupLevel(int groupLevel)
        {
            GroupLevel = groupLevel;
            return this;
        }

        public ViewOptions WithReduce(bool reduce)
        {
            Reduce = reduce;
            return this;
        }

        public ViewOptions WithDevelopment(bool development)
        {
            Development = development;
            return this;
        }

        public ViewOptions WithFullSet(bool fullSet)
        {
            FullSet = fullSet;
            return this;
        }

        public ViewOptions WithContinueOnError(bool continueOnError)
        {
            ContinueOnError = continueOnError;
            return this;
        }

        public ViewOptions WithConnectionTimeout(int connectionTimeout)
        {
            ConnectionTimeout = connectionTimeout;
            return this;
        }
    }

    #endregion

    #region Analytics Options

    public class AnalyticsOptions
    {
        public string ClientContextId { get; set; }
        public bool Pretty { get; set; }
        public bool IncludeMetrics { get; set; }
        public List<Tuple<string, string, bool>> Credentials { get; set; } = new List<Tuple<string, string, bool>>();
        public Dictionary<string, object> NamedParameters { get; set; } = new Dictionary<string, object>();
        public List<object> PositionalParameters { get; set; } = new List<object>();
        public TimeSpan? Timeout { get; set; }
        public int Priority { get; set; }
        public bool Deferred { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public AnalyticsOptions WithClientContextId(string clientContextId)
        {
            ClientContextId = clientContextId;
            return this;
        }

        public AnalyticsOptions WithPretty(bool pretty)
        {
            Pretty = pretty;
            return this;
        }

        public AnalyticsOptions WithIncludeMetrics(bool includeMetrics)
        {
            IncludeMetrics = includeMetrics;
            return this;
        }

        public AnalyticsOptions WithCredential(string username, string password, bool isAdmin)
        {
            Credentials.Add(Tuple.Create(username, password, isAdmin));
            return this;
        }

        public AnalyticsOptions WithNamedParameter(string parameterName, object value)
        {
            if (NamedParameters == null)
            {
                NamedParameters = new Dictionary<string, object>();
            }

            NamedParameters[parameterName] = value;
            return this;
        }

        public AnalyticsOptions WithPositionalParameter(object value)
        {
            if (PositionalParameters == null)
            {
                PositionalParameters = new List<object>();
            }

            PositionalParameters.Add(value);
            return this;
        }

        public AnalyticsOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public AnalyticsOptions WithPriority(bool priority)
        {
            Priority = priority ? -1 : 0;
            return this;
        }

        public AnalyticsOptions WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }

        public AnalyticsOptions WithDeferred(bool deferred)
        {
            Deferred = deferred;
            return this;
        }

        public AnalyticsOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }

    #endregion
}
