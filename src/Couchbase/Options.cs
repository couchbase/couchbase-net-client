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

        public TimeSpan? Timeout { get; set; }

        public CancellationToken Token { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public List<string> ProjectList { get; set; }

        public GetOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public GetOptions Project(params string[] fields)
        {
            if(ProjectList == null) ProjectList = new List<string>();
            ProjectList.AddRange(fields);
            return this;
        }

        public GetOptions WithCreatePath(bool createPath)
        {
            CreatePath = createPath;
            return this;
        }

        public GetOptions WithExpiration()
        {
            IncludeExpiration = true;
            return this;
        }
    }

    #endregion

    #region Exists Options

    public class ExistsOptions
    {
        public TimeSpan? Timeout { get; set; }

        public CancellationToken Token { get;set; }
    }

    #endregion

    #region Upsert Options

    public class UpsertOptions
    {
        public TimeSpan? Timeout { get; set; }

        public TimeSpan Expiration { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public CancellationToken Token { get; set; }
    }

    #endregion

    #region Insert Options

    public class InsertOptions
    {
        public TimeSpan? Timeout { get; set; }

        public TimeSpan Expiration { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public CancellationToken Token { get;set; }
    }

    #endregion

    #region Replace Options

    public class ReplaceOptions
    {
        public TimeSpan? Timeout { get; set; }

        public TimeSpan Expiration { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public CancellationToken Token { get; set; }
    }

    #endregion

    #region Remove Options

    public class RemoveOptions
    {
        public TimeSpan? Timeout { get; set; }

        public ulong Cas { get; set; }

        public ReplicateTo ReplicateTo { get; set; }

        public PersistTo PersistTo { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public CancellationToken Token { get; set; }
    }

    #endregion

    #region Unlock Options

    public class UnlockOptions
    {
        public TimeSpan? Timeout { get; set; }

        public ulong Cas { get; set; }

        public CancellationToken Token { get; set; }
    }

    #endregion

    #region Touch Options

    public class TouchOptions
    {
        public TimeSpan? Timeout { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public CancellationToken Token { get; set; }
    }

    #endregion

    #region Increment Options

    public class IncrementOptions
    {
        public TimeSpan Timeout { get; set; }

        public TimeSpan Expiration { get; set; }

        public ulong Initial { get; set; }

        public ulong Delta { get; set; }
    }

    #endregion

    #region Decrement options

    public class DecrementOptions
    {
        public TimeSpan Timeout { get; set; }

        public TimeSpan Expiration { get; set; }

        public ulong Initial { get; set; }

        public ulong Delta { get; set; }
    }
    #endregion

    #region Append Options

    public class AppendOptions
    {
        public TimeSpan Timeout { get; set; }

        public TimeSpan Expiration { get; set; }
    }

    #endregion

    #region Prepend Options

    public class PrependOptions
    {
        public TimeSpan Timeout { get; set; }

        public TimeSpan Expiration { get; set; }
    }
    
    #endregion

    #region GetAndLock Options

    public class GetAndLockOptions
    {
        public TimeSpan? Timeout { get; set; }

        public CancellationToken Token { get; set; }
    }

    #endregion

    #region GetAndTouch Options

    public class GetAndTouchOptions
    {
        public TimeSpan? Timeout { get; set; }

        public DurabilityLevel DurabilityLevel { get; set; }

        public CancellationToken Token { get; set; }
    }

    #endregion

    #region LookupInOptions

    public class LookupInOptions
    {
        internal TimeSpan _Timeout { get; set; }

        internal CancellationToken _Token { get; set; }

        public LookupInOptions Timeout(TimeSpan timeout)
        {
            _Timeout = timeout;
            return this;
        }

        public LookupInOptions Token(CancellationToken token)
        {
            _Token = token;
            return this;
        }
    }

    #endregion

    #region MutateInOptions

    public class MutateInOptions
    {
        internal TimeSpan _Timeout { get; set; }

        internal CancellationToken _Token { get; set; }

        internal TimeSpan _Expiration { get; set; }

        internal ulong _Cas { get; set; }

        internal SubdocDocFlags _Flags { get; set; }

        internal Tuple<PersistTo, ReplicateTo> _Durabilty { get; set; }

        internal DurabilityLevel _DurabilityLevel { get; set; }

        public MutateInOptions Timeout(TimeSpan timeout)
        {
            _Timeout = timeout;
            return this;
        }

        public MutateInOptions Timeout(int minutes = 0, int seconds = 0, int milliseconds=0)
        {
            return Timeout(new TimeSpan(0, 0, minutes, seconds, milliseconds));
        }

        public MutateInOptions Token(CancellationToken token)
        {
            _Token = token;
            return this;
        }

        public MutateInOptions Expiration(TimeSpan expiration)
        {
            _Expiration = expiration;
            return this;
        }

        public MutateInOptions Expiration(int days = 0, int hours = 0, int minutes = 0, int seconds = 0, int milliseconds=0)
        {
            return Expiration(new TimeSpan(days, hours, minutes, seconds, milliseconds));
        }

        public MutateInOptions Cas(ulong cas)
        {
            _Cas = cas;
            return this;
        }

        public MutateInOptions CreateDoc(bool createDoc)
        {
            if (createDoc)
            {
                _Flags = _Flags | SubdocDocFlags.InsertDocument;
            }
            return this;
        }

        public MutateInOptions Flags(SubdocDocFlags flags)
        {
            _Flags = flags;
            return this;
        }

        public MutateInOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            _Durabilty = new Tuple<PersistTo, ReplicateTo>(persistTo, replicateTo);
            return this;
        }

        public MutateInOptions Durability(DurabilityLevel durabilityLevel)
        {
            _DurabilityLevel = durabilityLevel;
            return this;
        }
    }

    #endregion
}
