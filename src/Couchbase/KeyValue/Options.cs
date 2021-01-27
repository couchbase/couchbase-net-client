using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;

#nullable enable

namespace Couchbase.KeyValue
{
    #region GetOptions

    public class GetOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        private static readonly ReadOnlyCollection<string> EmptyProjectList = new(Array.Empty<string>());
        internal static GetOptions Default { get; }

        static GetOptions()
        {
            // Initialize Default in a static constructor so we can ensure that it happens after EmptyProjectList
            // is initialized. Otherwise ProjectListValue may be null.
            Default = new GetOptions();
        }

        internal bool IncludeExpiryValue { get; private set; }

        internal ReadOnlyCollection<string> ProjectListValue { get; private set; } = EmptyProjectList;

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public GetOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        public GetOptions Expiry()
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            IncludeExpiryValue = true;
            return this;
        }

        public GetOptions Projection(params string[] fields)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            if (fields.Length > 0)
            {
                ProjectListValue = ProjectListValue.Count == 0
                    ? new ReadOnlyCollection<string>(fields)
                    : new ReadOnlyCollection<string>(ProjectListValue.Concat(fields).ToArray());
            }
            return this;
        }

        public GetOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public GetOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region GetAnyReplicaOptions

    public class GetAllReplicasOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        TimeSpan? ITimeoutOptions.Timeout => default;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public GetAllReplicasOptions Transcoder(ITypeTranscoder? transcoder)
        {
            if (ReferenceEquals(this, Default) && transcoder != null)
            {
                return new GetAllReplicasOptions
                {
                    TranscoderValue = transcoder
                };
            }

            TranscoderValue = transcoder;
            return this;
        }

        public GetAllReplicasOptions CancellationToken(CancellationToken token)
        {
            if (ReferenceEquals(this, Default) && token != default)
            {
                return new GetAllReplicasOptions
                {
                    TokenValue = token
                };
            }

            TokenValue = token;
            return this;
        }

        public static GetAllReplicasOptions Default { get; } = new();
    }

    #endregion

    #region GetAllReplicaOptions

    public class GetAnyReplicaOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        TimeSpan? ITimeoutOptions.Timeout => default;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public GetAnyReplicaOptions Transcoder(ITypeTranscoder? transcoder)
        {
            if (ReferenceEquals(this, Default) && transcoder != null)
            {
                return new GetAnyReplicaOptions
                {
                    TranscoderValue = transcoder
                };
            }

            TranscoderValue = transcoder;
            return this;
        }

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        public GetAnyReplicaOptions CancellationToken(CancellationToken token)
        {
            if (ReferenceEquals(this, Default) && token != default)
            {
                return new GetAnyReplicaOptions
                {
                    TokenValue = token
                };
            }

            TokenValue = token;
            return this;
        }

        public static GetAnyReplicaOptions Default { get; } = new();
    }

    #endregion

    #region Exists Options

    public class ExistsOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static ExistsOptions Default { get; } = new();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        public ExistsOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public ExistsOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Upsert Options

    public class UpsertOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static UpsertOptions Default { get; } = new();

        internal TimeSpan ExpiryValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public UpsertOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        public UpsertOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        public UpsertOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public UpsertOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public UpsertOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public UpsertOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Insert Options

    public class InsertOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static InsertOptions Default { get; } = new();

        internal TimeSpan ExpiryValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public InsertOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        public InsertOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        public InsertOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public InsertOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public InsertOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public InsertOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Replace Options

    public class ReplaceOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static ReplaceOptions Default { get; } = new();

        internal TimeSpan ExpiryValue { get; private set; }

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public ReplaceOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        public ReplaceOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        public ReplaceOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        public ReplaceOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public ReplaceOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public ReplaceOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public ReplaceOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Remove Options

    public class RemoveOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static RemoveOptions Default { get; } = new();

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        public RemoveOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        public RemoveOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public RemoveOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public RemoveOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public RemoveOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Unlock Options

    public class UnlockOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static UnlockOptions Default { get; } = new();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        public UnlockOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public UnlockOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Touch Options

    public class TouchOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static TouchOptions Default { get; } = new();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        public TouchOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public TouchOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Increment Options

    public class IncrementOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static IncrementOptions Default { get; } = new();

        internal ulong InitialValue { get; private set; } = 1;

        internal ulong DeltaValue { get; private set; } = 1;

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal TimeSpan ExpiryValue { get; private set; }

        /// <summary>
        /// The document's lifetime before being evicted by the server.
        /// </summary>
        /// <param name="expiry">The <see cref="TimeSpan"/> value for expiration</param>
        /// <returns>A <see cref="IncrementOptions"/> object.</returns>
        public IncrementOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        /// <summary>
        /// The initial value to begin incrementing.
        /// </summary>
        /// <param name="initial">The <see cref="ulong"/> value for the initial increment.</param>
        /// <returns>A <see cref="IncrementOptions"/> object for chaining options.</returns>
        public IncrementOptions Initial(ulong initial)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            InitialValue = initial;
            return this;
        }

        /// <summary>
        /// The value to increment the initial value by.
        /// </summary>
        /// <param name="delta">The <see cref="ulong"/> value to increment the initial value by.</param>
        /// <returns>A <see cref="IncrementOptions"/> object for chaining options.</returns>
        public IncrementOptions Delta(ulong delta)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DeltaValue = delta;
            return this;
        }

        /// <summary>
        /// The Compare And Swap or CAS value for optimistic locking.
        /// </summary>
        /// <param name="cas">A <see cref="ulong"/> value generated by the server by a previous operation.</param>
        /// <returns>A <see cref="IncrementOptions"/> object for chaining options.</returns>
        public IncrementOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        /// <summary>
        /// Settings to instruct Couchbase Server to update the specified document on multiple nodes in memory and/or disk
        /// locations across the cluster before considering the write to be committed.
        /// </summary>
        /// <param name="persistTo">The <see cref="PersistTo"/> durability requirement for persistence.</param>
        /// <param name="replicateTo">The <see cref="ReplicateTo"/> durability requirement for replication.</param>
        /// <returns>A <see cref="IncrementOptions"/> object for chaining options.</returns>
        public IncrementOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// Settings to instruct Couchbase Server to update the specified document on multiple nodes in memory and/or disk
        /// locations across the cluster before considering the write to be committed.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> requirement for replication and persistence.</param>
        /// <returns>A <see cref="IncrementOptions"/> object for chaining options.</returns>
        public IncrementOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time allowed for the operation before being terminated. This is controlled by the client; the default is 2.5s.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/> indicating the amount of time before the operations times out.</param>
        /// <returns>A <see cref="IncrementOptions"/> object for chaining options.</returns>
        public IncrementOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A token for cooperative cancellation of the operation between threads.
        /// </summary>
        /// <param name="token">A <see cref="CancellationToken"/>; if not supplied and internal token will handle cancellation.</param>
        /// <returns>A <see cref="IncrementOptions"/> object for chaining options.</returns>
        public IncrementOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Decrement options

    public class DecrementOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static DecrementOptions Default { get; } = new();

        internal ulong InitialValue { get; private set; } = 1;

        internal ulong DeltaValue { get; private set; } = 1;

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal TimeSpan ExpiryValue { get; private set; }

        public DecrementOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        public DecrementOptions Initial(ulong initial)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            InitialValue = initial;
            return this;
        }

        public DecrementOptions Delta(ulong delta)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DeltaValue = delta;
            return this;
        }

        public DecrementOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        public DecrementOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public DecrementOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public DecrementOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public DecrementOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Append Options

    public class AppendOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static AppendOptions Default { get; } = new();

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        public AppendOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        public AppendOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public AppendOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public AppendOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public AppendOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region Prepend Options

    public class PrependOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static PrependOptions Default { get; } = new();

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        public PrependOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        public PrependOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        public PrependOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public PrependOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public PrependOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region GetAndLock Options

    public class GetAndLockOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static GetAndLockOptions Default { get; } = new();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public GetAndLockOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        public GetAndLockOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public GetAndLockOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region GetAndTouch Options

    public class GetAndTouchOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static GetAndTouchOptions Default { get; } = new();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        public GetAndTouchOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        public GetAndTouchOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public GetAndTouchOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }
    }

    #endregion

    #region LookupInOptions

    public class LookupInOptions : IKeyValueOptions, ITimeoutOptions, ITranscoderOverrideOptions
    {
        internal static LookupInOptions Default { get; } = new();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal bool ExpiryValue { get; private set; }

        internal ITypeSerializer? SerializerValue { get; private set; }

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        internal bool AccessDeletedValue { get; private set; }

        public LookupInOptions Serializer(ITypeSerializer? serializer)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            SerializerValue = serializer;
            return this;
        }

        /// <summary>
        /// Only used internally for full doc gets which also need the expiry. Should not be used for JSON-based LookupIn ops.
        /// Not exposed for public consumption.
        /// </summary>
        internal LookupInOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        public LookupInOptions Timeout(TimeSpan? timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public LookupInOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public LookupInOptions Expiry(bool expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        public LookupInOptions AccessDeleted(bool accessDeleted)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            AccessDeletedValue = accessDeleted;
            return this;
        }
    }

    #endregion

    #region MutateInOptions

    public class MutateInOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static MutateInOptions Default { get; } = new();

        internal TimeSpan ExpiryValue { get; private set; }

        internal StoreSemantics StoreSemanticsValue { get; private set; }

        internal ulong CasValue { get; private set; }

        internal ValueTuple<PersistTo, ReplicateTo> DurabilityValue { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeSerializer? SerializerValue { get; private set; }

        internal bool CreateAsDeletedValue { get; private set; }

        internal bool AccessDeletedValue { get; private set; }

        public MutateInOptions StoreSemantics(StoreSemantics storeSemantics)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            StoreSemanticsValue = storeSemantics;
            return this;
        }

        public MutateInOptions Serializer(ITypeSerializer? serializer)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            SerializerValue = serializer;
            return this;
        }

        public MutateInOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        public MutateInOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        public MutateInOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityValue = new ValueTuple<PersistTo, ReplicateTo>(persistTo, replicateTo);
            return this;
        }

        public MutateInOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        public MutateInOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        public MutateInOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public MutateInOptions CreateAsDeleted(bool createAsDeleted)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CreateAsDeletedValue = createAsDeleted;
            return this;
        }

        /// <summary>
        /// Allows access to a deleted document's attributes section.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        public MutateInOptions AccessDeleted(bool accessDeleted)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            AccessDeletedValue = accessDeleted;
            return this;
        }
    }

    #endregion

    #region MutateIn Options

    public abstract class MutateInXattrOperation : IKeyValueOptions
    {
        internal bool XAttrValue { get; private set; }

        public MutateInXattrOperation XAttr()
        {
            XAttrValue = true;
            return this;
        }
    }

    public abstract class MutateInOperationOptions :  MutateInXattrOperation
    {
        internal bool CreatePathValue { get; private set; }

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
