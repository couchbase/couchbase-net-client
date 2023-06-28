using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.KeyValue
{
    #region GetOptions

    /// <summary>
    /// Optional parameters for <see cref="ICouchbaseCollection.GetAsync(string, GetOptions?)"/>
    /// </summary>
    public class GetOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        private static readonly ReadOnlyCollection<string> EmptyProjectList = new(Array.Empty<string>());
        internal static GetOptions Default { get; }
        public static readonly ReadOnly DefaultReadOnly;

        static GetOptions()
        {
            // Initialize Default in a static constructor so we can ensure that it happens after EmptyProjectList
            // is initialized. Otherwise ProjectListValue may be null.
            Default = new GetOptions();
            DefaultReadOnly = Default.AsReadOnly();
        }

        /// <summary>
        /// Used internally to ensure that <see cref="DocumentNotFoundException"/> is not thrown
        /// when the server returns KeyNotFound for the status.
        /// </summary>
        internal bool PreferReturn { get; set; }

        internal bool IncludeExpiryValue { get; private set; }

        internal ReadOnlyCollection<string> ProjectListValue { get; private set; } = EmptyProjectList;

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>A <see cref="GetOptions"/> instance for chaining.</returns>
        public GetOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>A <see cref="GetOptions"/> instance for chaining.</returns>
        public GetOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns></returns>
        public GetOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// The time for the key/value pair to exist on the server.
        /// </summary>
        /// <returns>A <see cref="GetOptions"/> instance for chaining.</returns>
        public GetOptions Expiry()
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            IncludeExpiryValue = true;
            return this;
        }

        /// <summary>
        /// A list or array of fields to project - if called will switch to subdoc and only fetch the fields requested.
        /// If the number of fields is > 16, then it will perform a full-doc lookup instead.
        /// </summary>
        /// <param name="fields"></param>
        /// <returns>A <see cref="GetOptions"/> instance for chaining.</returns>
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

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>A <see cref="GetOptions"/> instance for chaining.</returns>
        public GetOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A <see cref="GetOptions"/> instance for chaining.</returns>
        public GetOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out bool includeExpiry, out ReadOnlyCollection<string> projectList, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out bool preferReturn)
        {
            includeExpiry = IncludeExpiryValue;
            projectList = ProjectListValue;
            timeout = TimeoutValue;
            token = TokenValue;
            transcoder = TranscoderValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
            preferReturn = PreferReturn;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out bool includeExpiry, out ReadOnlyCollection<string> projectList, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out bool preferReturn);
            return new ReadOnly(includeExpiry, projectList, timeout, token, transcoder, timeout, retryStrategy, requestSpan, preferReturn);
        }

        public record ReadOnly(
            bool IncludeExpiry,
            ReadOnlyCollection<string> ProjectList,
            TimeSpan? Timeout,
            CancellationToken Token,
            ITypeTranscoder? Transcoder,
            TimeSpan? TimeSpan,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan,
            bool PreferReturn);
    }

    #endregion

    #region GetAnyReplicaOptions

    /// <summary>
    /// Optional parameters for <see cref="ICouchbaseCollection.GetAllReplicasAsync(string, GetAllReplicasOptions?)"/>
    /// </summary>
    public class GetAllReplicasOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        public static readonly ReadOnly DefaultReadOnly = new GetAllReplicasOptions().AsReadOnly();
        TimeSpan? ITimeoutOptions.Timeout => default;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>A <see cref="GetAllReplicasOptions"/> instance for chaining.</returns>
        public GetAllReplicasOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>A <see cref="GetAllReplicasOptions"/> instance for chaining.</returns>
        public GetAllReplicasOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>A <see cref="GetAllReplicasOptions"/> instance for chaining.</returns>
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

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A <see cref="GetAllReplicasOptions"/> instance for chaining.</returns>
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

        public void Deconstruct(out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            token = TokenValue;
            transcoder = TranscoderValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(token, transcoder, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            CancellationToken Token,
            ITypeTranscoder? Transcoder,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region GetAllReplicaOptions

    /// <summary>
    /// Optional parameters for <see cref="ICouchbaseCollection.GetAnyReplicaAsync(string, GetAnyReplicaOptions?)"/>
    /// </summary>
    public class GetAnyReplicaOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        public static readonly ReadOnly DefaultReadOnly = new GetAnyReplicaOptions().AsReadOnly();
        TimeSpan? ITimeoutOptions.Timeout => default;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        public GetAnyReplicaOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>A <see cref="GetAnyReplicaOptions"/> instance for chaining.</returns>
        public GetAnyReplicaOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>A <see cref="GetAnyReplicaOptions"/> instance for chaining.</returns>
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

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A <see cref="GetAnyReplicaOptions"/> instance for chaining.</returns>
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

        public void Deconstruct(out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out CancellationToken token)
        {
            transcoder = TranscoderValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
            token = TokenValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out CancellationToken token);
            return new ReadOnly(transcoder, retryStrategy, requestSpan, token);
        }

        public record ReadOnly(
            ITypeTranscoder? Transcoder,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan,
            CancellationToken Token);
    }

    #endregion

    #region Exists Options

    /// <summary>
    /// Optional parameters for <see cref="ICouchbaseCollection.ExistsAsync(string, ExistsOptions?)"/>
    /// </summary>
    public class ExistsOptions : ITimeoutOptions
    {
        internal static ExistsOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>A <see cref="ExistsOptions"/> instance for chaining.</returns>
        public ExistsOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public ExistsOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public ExistsOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public ExistsOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            timeout = TimeoutValue;
            token = TokenValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(timeout, token, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            TimeSpan? Timeout,
            CancellationToken Token,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region Upsert Options

    public class UpsertOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        public static readonly ReadOnly DefaultReadOnly = new UpsertOptions().AsReadOnly();

        internal static UpsertOptions Default { get; } = new();

        internal TimeSpan ExpiryValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        internal bool PreserveTtlValue { get; private set; }

        /// <summary>
        /// Specifies whether an existing document's expiry should be preserved.
        /// If true, and the document exists, its expiry will not be modified.Otherwise
        /// the document's expiry is determined by <see cref="Expiry"/>.
        /// </summary>
        /// <remarks>The default is false.</remarks>
        /// <param name="preserveTtl"></param>
        /// <returns>An options object for chaining.</returns>
        public UpsertOptions PreserveTtl(bool preserveTtl)
        {
            PreserveTtlValue = preserveTtl;
            return this;
        }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>A <see cref="UpsertOptions"/> instance for chaining.</returns>
        public UpsertOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>A <see cref="UpsertOptions"/> instance for chaining.</returns>
        public UpsertOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>A <see cref="UpsertOptions"/> instance for chaining.</returns>
        public UpsertOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// Sets the expiration for a document. By default, documents never expire - if overridden the value must be less than 50 years.
        /// </summary>
        /// <param name="expiry">The expiration for a document.</param>
        /// <returns>An options instance for chaining.</returns>
        public UpsertOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An options instance for chaining.</returns>
        public UpsertOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public UpsertOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>A <see cref="UpsertOptions"/> instance for chaining.</returns>
        public UpsertOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A <see cref="UpsertOptions"/> instance for chaining.</returns>
        public UpsertOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan expiry, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out bool preserveTtl, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder)
        {
            expiry = ExpiryValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
            preserveTtl = PreserveTtlValue;
            timeout = TimeoutValue;
            token = TokenValue;
            transcoder = TranscoderValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan expiry, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out bool preserveTtl, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder);
            return new ReadOnly(expiry, replicateTo, persistTo, durabilityLevel, retryStrategy, requestSpan, preserveTtl, timeout, token, transcoder);
        }

        public record ReadOnly(
            TimeSpan Expiry,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan,
            bool PreserveTtl,
            TimeSpan? Timeout,
            CancellationToken Token,
            ITypeTranscoder? Transcoder);
    }

    #endregion

    #region Insert Options

    public class InsertOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        public static readonly ReadOnly DefaultReadOnly = new InsertOptions().AsReadOnly();

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

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        public InsertOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>A <see cref="InsertOptions"/> instance for chaining.</returns>
        public InsertOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>A <see cref="InsertOptions"/> instance for chaining.</returns>
        public InsertOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// Sets the expiration for a document. By default, documents never expire - if overridden the value must be less than 50 years.
        /// </summary>
        /// <param name="expiry">The time-to-live of the document.</param>
        /// <returns>An options object for chaining.</returns>
        public InsertOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }


        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An options instance for chaining.</returns>
        public InsertOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public InsertOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>A <see cref="InsertOptions"/> instance for chaining.</returns>
        public InsertOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A <see cref="UpsertOptions"/> instance for chaining.</returns>
        public InsertOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan expiry, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            expiry = ExpiryValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            timeout = TimeoutValue;
            token = TokenValue;
            transcoder = TranscoderValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan expiry, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(expiry, replicateTo, persistTo, durabilityLevel, timeout, token, transcoder, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            TimeSpan Expiry,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            TimeSpan? Timeout,
            CancellationToken Token,
            ITypeTranscoder? Transcoder,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region Replace Options

    public class ReplaceOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static ReplaceOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

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

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        internal bool PreserveTtlValue { get; private set; }

        /// <summary>
        /// Specifies whether an existing document's expiry should be preserved.
        /// If true, and the document exists, its expiry will not be modified.Otherwise
        /// the document's expiry is determined by <see cref="Expiry"/>.
        /// </summary>
        /// <remarks>The default is false.</remarks>
        /// <param name="preserveTtl"></param>
        /// <returns>An options object for chaining.</returns>
        public ReplaceOptions PreserveTtl(bool preserveTtl)
        {
            PreserveTtlValue = preserveTtl;
            return this;
        }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>A <see cref="ExistsOptions"/> instance for chaining.</returns>
        public ReplaceOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public ReplaceOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>An options instance for chaining.</returns>
        public ReplaceOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// Sets the expiration for a document. By default, documents never expire -
        /// if overridden the value must be less than 50 years.
        /// </summary>
        /// <param name="expiry">The time-to-live of the document.</param>
        /// <returns>An options object for chaining.</returns>
        public ReplaceOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        /// <summary>
        /// Compare and Set value for optimistic locking of a document.
        /// </summary>
        /// <param name="cas">A <see cref="ulong"/> value returned by the server in a previous operation.</param>
        /// <returns>An options object for chaining.</returns>
        public ReplaceOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An options instance for chaining.</returns>
        public ReplaceOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public ReplaceOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public ReplaceOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public ReplaceOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan expiry, out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategyValue, out IRequestSpan? requestSpan, out bool preserveTtl)
        {
            expiry = ExpiryValue;
            cas = CasValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            timeout = TimeoutValue;
            token = TokenValue;
            transcoder = TranscoderValue;
            retryStrategyValue = RetryStrategyValue;
            requestSpan = RequestSpanValue;
            preserveTtl = PreserveTtlValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan expiry, out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategyValue, out IRequestSpan? requestSpan, out bool preserveTtl);
            return new ReadOnly(expiry, cas, replicateTo, persistTo, durabilityLevel, timeout, token, transcoder, retryStrategyValue, requestSpan, preserveTtl);
        }

        public record ReadOnly(
            TimeSpan Expiry,
            ulong Cas,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            TimeSpan? Timeout,
            CancellationToken Token,
            ITypeTranscoder? Transcoder,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan,
            bool PreserveTtl);
    }

    #endregion

    #region Remove Options

    public class RemoveOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static RemoveOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public RemoveOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public RemoveOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Compare and Set value for optimistic locking of a document.
        /// </summary>
        /// <param name="cas">A <see cref="ulong"/> value returned by the server in a previous operation.</param>
        /// <returns>An options object for chaining.</returns>
        public RemoveOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An options instance for chaining.</returns>
        public RemoveOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public RemoveOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public RemoveOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public RemoveOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            cas = CasValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            timeout = TimeoutValue;
            token = TokenValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(cas, replicateTo, persistTo, durabilityLevel, timeout, token, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            ulong Cas,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            TimeSpan? Timeout,
            CancellationToken Token,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region Unlock Options

    public class UnlockOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static UnlockOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public UnlockOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public UnlockOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public UnlockOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public UnlockOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            timeout = TimeoutValue;
            token = TokenValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(timeout, token, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            TimeSpan? Timeout,
            CancellationToken Token,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region Touch Options

    public class TouchOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static TouchOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public TouchOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public TouchOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public TouchOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public TouchOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            timeout = TimeoutValue;
            token = TokenValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(timeout, token, retryStrategy, requestSpan);
        }


        public record ReadOnly(
            TimeSpan? Timeout,
            CancellationToken Token,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region Increment Options

    public class IncrementOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static IncrementOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal ulong InitialValue { get; private set; } = 1;

        internal ulong DeltaValue { get; private set; } = 1;

        [Obsolete("CAS is not supported by the server for the Increment operation.")]
        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public IncrementOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public IncrementOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal TimeSpan ExpiryValue { get; private set; }

        /// <summary>
        /// The document's lifetime before being evicted by the server. By default the operation will never expire - must be set for a value less than 50 years.
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
        [Obsolete("CAS is not supported by the server for the Increment operation.")]
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

        public void Deconstruct(out ulong initial, out ulong delta, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out TimeSpan? timeout, out CancellationToken token, out TimeSpan expiry)
        {
            initial = InitialValue;
            delta = DeltaValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
            timeout = TimeoutValue;
            token = TokenValue;
            expiry = ExpiryValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out ulong initial, out ulong delta, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out TimeSpan? timeout, out CancellationToken token, out TimeSpan expiry);
            return new ReadOnly(initial, delta, replicateTo, persistTo, durabilityLevel, retryStrategy, requestSpan, timeout, token, expiry);
        }

        public record ReadOnly(
            ulong Initial,
            ulong Delta,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan,
            TimeSpan? Timeout,
            CancellationToken Token,
            TimeSpan Expiry);
    }

    #endregion

    #region Decrement options

    public class DecrementOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static DecrementOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal ulong InitialValue { get; private set; } = 1;

        internal ulong DeltaValue { get; private set; } = 1;

        [Obsolete("CAS is not supported by the server for the Increment operation.")]
        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal TimeSpan ExpiryValue { get; private set; }

        internal IRequestSpan? RequestSpanValue { get; private set; }

        public DecrementOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public DecrementOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Sets the expiration for a document. By default, documents never expire - if overridden the value must be less than 50 years.
        /// </summary>
        /// <param name="expiry">The time-to-live of the document.</param>
        /// <returns>An options object for chaining.</returns>
        public DecrementOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        /// <summary>
        /// The initial value to start from.
        /// </summary>
        /// <param name="initial">A <see cref="ulong"/> inital value.</param>
        // <returns>An options object for chaining.</returns>
        public DecrementOptions Initial(ulong initial)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            InitialValue = initial;
            return this;
        }

        /// <summary>
        /// The value to decrement by.
        /// </summary>
        /// <param name="delta">A <see cref="ulong"/> value to decrement by..</param>
        // <returns>An options object for chaining.</returns>
        public DecrementOptions Delta(ulong delta)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DeltaValue = delta;
            return this;
        }

        [Obsolete("CAS is not supported by the server for the Increment operation.")]
        public DecrementOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An options instance for chaining.</returns>
        public DecrementOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public DecrementOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public DecrementOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public DecrementOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out ulong initial, out ulong delta, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out TimeSpan expiry, out IRequestSpan? requestSpan, out IRetryStrategy? retryStrategy)
        {
            initial = InitialValue;
            delta = DeltaValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            timeout = TimeoutValue;
            token = TokenValue;
            expiry = ExpiryValue;
            requestSpan = RequestSpanValue;
            retryStrategy = RetryStrategyValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out ulong initial, out ulong delta, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out TimeSpan expiry, out IRequestSpan? requestSpan, out IRetryStrategy? retryStrategy);
            return new ReadOnly(initial, delta, replicateTo, persistTo, durabilityLevel, timeout, token, expiry, requestSpan, retryStrategy);
        }

        public record ReadOnly(
            ulong Initial,
            ulong Delta,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            TimeSpan? Timeout,
            CancellationToken Token,
            TimeSpan Expiry,
            IRequestSpan? RequestSpan,
            IRetryStrategy? RetryStrategy);
    }

    #endregion

    #region Append Options

    public class AppendOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static AppendOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public AppendOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public AppendOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Compare and Set value for optimistic locking of a document.
        /// </summary>
        /// <param name="cas">A <see cref="ulong"/> value returned by the server in a previous operation.</param>
        /// <returns>An options object for chaining.</returns>
        public AppendOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An options instance for chaining.</returns>
        public AppendOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            PersistTo = persistTo;
            ReplicateTo = replicateTo;
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public AppendOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public AppendOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public AppendOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            cas = CasValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            timeout = TimeoutValue;
            token = TokenValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(cas, replicateTo, persistTo, durabilityLevel, timeout, token, retryStrategy, requestSpan);
        }


        public record ReadOnly(
            ulong Cas,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            TimeSpan? Timeout,
            CancellationToken Token,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region Prepend Options

    public class PrependOptions : IKeyValueOptions, ITimeoutOptions
    {
        internal static PrependOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal ulong CasValue { get; private set; }

        internal ReplicateTo ReplicateTo { get; private set; }

        internal PersistTo PersistTo { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public PrependOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public PrependOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Compare and Set value for optimistic locking of a document.
        /// </summary>
        /// <param name="cas">A <see cref="ulong"/> value returned by the server in a previous operation.</param>
        /// <returns>An options object for chaining.</returns>
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

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public PrependOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public PrependOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }


        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public PrependOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            cas = CasValue;
            replicateTo = ReplicateTo;
            persistTo = PersistTo;
            durabilityLevel = DurabilityLevel;
            timeout = TimeoutValue;
            token = TokenValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out ulong cas, out ReplicateTo replicateTo, out PersistTo persistTo, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(cas, replicateTo, persistTo, durabilityLevel, timeout, token, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            ulong Cas,
            ReplicateTo ReplicateTo,
            PersistTo PersistTo,
            DurabilityLevel DurabilityLevel,
            TimeSpan? Timeout,
            CancellationToken Token,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region GetAndLock Options

    public class GetAndLockOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static GetAndLockOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndLockOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndLockOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndLockOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndLockOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndLockOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            timeout = TimeoutValue;
            token = TokenValue;
            transcoder = TranscoderValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(timeout, token, transcoder, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            TimeSpan? Timeout,
            CancellationToken Token,
            ITypeTranscoder? Transcoder,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region GetAndTouch Options

    public class GetAndTouchOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static GetAndTouchOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndTouchOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndTouchOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndTouchOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndTouchOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public GetAndTouchOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        public void Deconstruct(out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            timeout = TimeoutValue;
            token = TokenValue;
            transcoder = TranscoderValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan? timeout, out CancellationToken token, out ITypeTranscoder? transcoder, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(timeout, token, transcoder, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            TimeSpan? Timeout,
            CancellationToken Token,
            ITypeTranscoder? Transcoder,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region LookupInOptions

    public class LookupInOptions : IKeyValueOptions, ITimeoutOptions, ITranscoderOverrideOptions
    {
        internal static LookupInOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal bool ExpiryValue { get; private set; }

        internal ITypeSerializer? SerializerValue { get; private set; }

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        internal bool AccessDeletedValue { get; private set; }

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public LookupInOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public LookupInOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        ///A custom <see cref="ITypeSerializer"/> implementation for serialization.
        /// </summary>
        /// <param name="serializer">A custom <see cref="ITypeSerializer"/> implementation for serialization.</param>
        /// <returns>An options instance for chaining.</returns>
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
        public LookupInOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public LookupInOptions Timeout(TimeSpan? timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
        public LookupInOptions CancellationToken(CancellationToken token)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TokenValue = token;
            return this;
        }

        /// <summary>
        /// Sets the expiration for a document. By default, documents never expire - if overridden the value must be less than 50 years.
        /// </summary>
        /// <param name="expiry">The time-to-live of the document.</param>
        /// <returns>An options object for chaining.</returns>
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

        public void Deconstruct(out TimeSpan? timeout, out CancellationToken token, out bool expiry, out ITypeSerializer? serializer, out ITypeTranscoder? transcoder, out bool accessDeleted, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan)
        {
            timeout = TimeoutValue;
            token = TokenValue;
            expiry = ExpiryValue;
            serializer = SerializerValue;
            transcoder = TranscoderValue;
            accessDeleted = AccessDeletedValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan? timeout, out CancellationToken token, out bool expiry, out ITypeSerializer? serializer, out ITypeTranscoder? transcoder, out bool accessDeleted, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan);
            return new ReadOnly(timeout, token, expiry, serializer, transcoder, accessDeleted, retryStrategy, requestSpan);
        }

        public record ReadOnly(
            TimeSpan? Timeout,
            CancellationToken Token,
            bool Expiry,
            ITypeSerializer? Serializer,
            ITypeTranscoder? Transcoder,
            bool AccessDeleted,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
    }

    #endregion

    #region MutateInOptions

    public class MutateInOptions : ITranscoderOverrideOptions, IKeyValueOptions, ITimeoutOptions
    {
        internal static MutateInOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        internal TimeSpan ExpiryValue { get; private set; }

        internal StoreSemantics StoreSemanticsValue { get; private set; }

        internal ulong CasValue { get; private set; }

        internal ValueTuple<PersistTo, ReplicateTo> DurabilityValue { get; private set; }

        internal DurabilityLevel DurabilityLevel { get; private set; }

        internal TimeSpan? TimeoutValue { get; private set; }
        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        internal CancellationToken TokenValue { get; private set; }
        CancellationToken ITimeoutOptions.Token => TokenValue;

        internal bool CreateAsDeletedValue { get; private set; }

        internal bool AccessDeletedValue { get; private set; }

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        internal bool PreserveTtlValue { get; private set; }

        internal ITypeTranscoder? TranscoderValue { get; private set; }
        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        /// <summary>
        /// Specifies whether an existing document's expiry should be preserved.
        /// If true, and the document exists, its expiry will not be modified.Otherwise
        /// the document's expiry is determined by <see cref="Expiry"/>.
        /// </summary>
        /// <remarks>The default is false.</remarks>
        /// <param name="preserveTtl"></param>
        /// <returns>An options object for chaining.</returns>
        public MutateInOptions PreserveTtl(bool preserveTtl)
        {
            PreserveTtlValue = preserveTtl;
            return this;
        }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// StoreSemantics - the storage action
        /// Replace - replace the document, fail if it doesn't exist
        /// Upsert - replace the document or create it if it doesn't exist (0x01)
        /// Insert - create document, fail if it exists(0x02)
        /// </summary>
        /// <param name="storeSemantics"></param>
        /// <returns></returns>
        public MutateInOptions StoreSemantics(StoreSemantics storeSemantics)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            StoreSemanticsValue = storeSemantics;
            return this;
        }

        /// <summary>
        ///A custom <see cref="ITypeSerializer"/> implementation for serialization.
        /// </summary>
        /// <param name="serializer">A custom <see cref="ITypeSerializer"/> implementation for serialization.</param>
        /// <returns>An options instance for chaining.</returns>
        [Obsolete("Use Transcoder instead.")]
        public MutateInOptions Serializer(ITypeSerializer? serializer)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            return this;
        }

        /// <summary>
        /// Inject a <see cref="ITypeTranscoder"/> other than the default <see cref="JsonTranscoder"/>.
        /// </summary>
        /// <param name="transcoder"></param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInOptions Transcoder(ITypeTranscoder? transcoder)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// Sets the expiration for a document. By default, documents never expire - if overridden the value must be less than 50 years.
        /// </summary>
        /// <param name="expiry">The time-to-live of the document.</param>
        /// <returns>An options object for chaining.</returns>
        public MutateInOptions Expiry(TimeSpan expiry)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            ExpiryValue = expiry;
            return this;
        }

        /// <summary>
        /// Compare and Set value for optimistic locking of a document.
        /// </summary>
        /// <param name="cas">A <see cref="ulong"/> value returned by the server in a previous operation.</param>
        /// <returns>An options object for chaining.</returns>
        public MutateInOptions Cas(ulong cas)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            CasValue = cas;
            return this;
        }


        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInOptions Durability(PersistTo persistTo, ReplicateTo replicateTo)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityValue = new ValueTuple<PersistTo, ReplicateTo>(persistTo, replicateTo);
            return this;
        }

        /// <summary>
        /// The durability level required for persisting a JSON document across the cluster.
        /// </summary>
        /// <param name="durabilityLevel">The <see cref="DurabilityLevel"/> required for persistance.</param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInOptions Durability(DurabilityLevel durabilityLevel)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// The time in which the operation will timeout if it does not complete.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInOptions Timeout(TimeSpan timeout)
        {
            Debug.Assert(!ReferenceEquals(this, Default), "Default should be immutable");
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>An options instance for chaining.</returns>
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

        public void Deconstruct(out TimeSpan expiry, out StoreSemantics storeSemantics, out ulong cas, out (PersistTo, ReplicateTo) durability, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out ITypeSerializer? serializer, out bool createAsDeleted, out bool accessDeleted, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out bool preserveTtl, out ITypeTranscoder? transcoder)
        {
            expiry = ExpiryValue;
            storeSemantics = StoreSemanticsValue;
            cas = CasValue;
            durability = DurabilityValue;
            durabilityLevel = DurabilityLevel;
            timeout = TimeoutValue;
            token = TokenValue;
            serializer = TranscoderValue?.Serializer;
            createAsDeleted = CreateAsDeletedValue;
            accessDeleted = AccessDeletedValue;
            retryStrategy = RetryStrategyValue;
            requestSpan = RequestSpanValue;
            preserveTtl = PreserveTtlValue;
            transcoder = TranscoderValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out TimeSpan expiry, out StoreSemantics storeSemantics, out ulong cas, out (PersistTo, ReplicateTo) durability, out DurabilityLevel durabilityLevel, out TimeSpan? timeout, out CancellationToken token, out ITypeSerializer? serializer, out bool createAsDeleted, out bool accessDeleted, out IRetryStrategy? retryStrategy, out IRequestSpan? requestSpan, out bool preserveTtl, out ITypeTranscoder? transcoder);
            return new ReadOnly(expiry, storeSemantics, cas, durability, durabilityLevel, timeout, token, serializer, createAsDeleted, accessDeleted, retryStrategy, requestSpan, preserveTtl, transcoder);
        }

        public record ReadOnly(
            TimeSpan Expiry,
            StoreSemantics StoreSemantics,
            ulong Cas,
            (PersistTo, ReplicateTo) Durability,
            DurabilityLevel DurabilityLevel,
            TimeSpan? Timeout,
            CancellationToken Token,
            ITypeSerializer? Serializer,
            bool CreateAsDeleted,
            bool AccessDeleted,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan,
            bool PreserveTtl,
            ITypeTranscoder? Transcoder);
    }

    #endregion

    #region MutateIn Options

    public abstract class MutateInXattrOperation : IKeyValueOptions
    {
        internal bool XAttrValue { get; private set; }

        internal IRetryStrategy? RetryStrategyValue { get; private set; }
        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="span">An <see cref="IRequestSpan"/></param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInXattrOperation RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Inject a custom <see cref="IRetryStrategy"/>.
        /// </summary>
        /// <param name="retryStrategy"></param>
        /// <returns>An options instance for chaining.</returns>
        public MutateInXattrOperation RetryStrategy(IRetryStrategy retryStrategy)
        {
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// If true then this is an xattr operation.
        /// </summary>
        /// <returns>An options instance for chaining.</returns>
        public MutateInXattrOperation XAttr()
        {
            XAttrValue = true;
            return this;
        }
    }

    public abstract class MutateInOperationOptions :  MutateInXattrOperation
    {
        internal bool CreatePathValue { get; private set; }

        /// <summary>
        /// Create the path if it doesn't exist.
        /// </summary>
        /// <returns>An options instance for chaining.</returns>
        public MutateInOperationOptions CreatePath()
        {
            CreatePathValue = true;
            return this;
        }
    }


    /// <inheritdoc />
    public sealed class MutateInInsertOptions : MutateInOperationOptions {}

    /// <inheritdoc />
    public sealed class MutateInUpsertOptions : MutateInOperationOptions {}

    /// <inheritdoc />
    public sealed class MutateInReplaceOptions : MutateInXattrOperation {}

    /// <inheritdoc />
    public sealed class MutateInRemoveOptions : MutateInXattrOperation {}

    /// <inheritdoc />
    public sealed class MutateInArrayAppendOptions : MutateInOperationOptions {}

    public sealed class MutateInArrayPrependOptions :MutateInOperationOptions {}

    /// <inheritdoc />
    public sealed class MutateInArrayInsertOptions : MutateInOperationOptions {}

    /// <inheritdoc />
    public sealed class MutateInArrayAddUniqueOptions : MutateInOperationOptions {}

    /// <inheritdoc />
    public sealed class MutateInIncrementOptions : MutateInOperationOptions {}

    /// <inheritdoc />
    public sealed class MutateInDecrementOptions : MutateInOperationOptions {}

    #endregion
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
