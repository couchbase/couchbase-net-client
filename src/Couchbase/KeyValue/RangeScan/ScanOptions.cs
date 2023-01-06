using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Query;
using System;
using System.Threading;

#nullable enable

namespace Couchbase.KeyValue.RangeScan
{
    /// <summary>
    /// Options for a KV Range Scan.
    /// </summary>
    public class ScanOptions : ITranscoderOverrideOptions, ITimeoutOptions
    {
        internal static ScanOptions Default { get; }

        static ScanOptions()
        {
            Default = new ScanOptions();
        }

        #region Internal Accessors

        internal TimeSpan? TimeoutValue { get; set; }

        internal ITypeTranscoder? TranscoderValue { get; set; }

        internal IRetryStrategy? RetryStrategyValue { get; set; }

        internal IRequestSpan? ParentSpanValue { get; set; }

        internal bool IdsOnlyValue { get; set; }

        internal MutationState? ConsistentWithValue { get; set; }

        internal ScanSort SortValue { get; set; }

        internal CancellationToken TokenValue { get; private set; }

        internal uint BatchItemLimit { get; set; } = 0;

        internal uint BatchByteLimit { get; set; } = 0;

        internal uint BatchTimeLimit { get; set; } = 0;

        ITypeTranscoder? ITranscoderOverrideOptions.Transcoder => TranscoderValue;

        IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

        TimeSpan? ITimeoutOptions.Timeout => TimeoutValue;

        CancellationToken ITimeoutOptions.Token => TokenValue;

        #endregion

        #region Public setters

        /// <summary>
        /// The timeout for the scan.
        /// </summary>
        /// <param name="timeSpan">A <see cref="TimeSpan"/> value specifying when the scan will timeout.</param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions Timeout(TimeSpan timeSpan)
        {
            TimeoutValue = timeSpan;
            return this;
        }

        /// <summary>
        /// Override the default transcoder.
        /// </summary>
        /// <param name="transcoder">A <see cref="ITypeTranscoder"/> instance. </param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions Transcoder(ITypeTranscoder transcoder)
        {
            TranscoderValue = transcoder;
            return this;
        }

        /// <summary>
        /// Override the default retry strategy.
        /// </summary>
        /// <param name="retryStrategy">A <see cref="IRetryStrategy"/> instance.</param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            RetryStrategyValue = retryStrategy;
            return this;
        }

        /// <summary>
        /// Inject an external span which will the be the parent span of the internal span(s).
        /// </summary>
        /// <param name="parentSpan">An <see cref="IRequestSpan"/></param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions ParentSpan(IRequestSpan parentSpan)
        {
            ParentSpanValue = parentSpan;
            return this;
        }

        /// <summary>
        /// Do not return content.
        /// </summary>
        /// <param name="withoutContent">True to not send content.</param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions IdsOnly(bool withoutContent)
        {
            IdsOnlyValue = withoutContent;
            return this;
        }

        /// <summary>
        /// Provides a means of ensuring "read your own writes" or RYOW consistency on the current query.
        /// </summary>
        /// <param name="mutationState"></param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions ConsistentWith(MutationState mutationState)
        {
            ConsistentWithValue = mutationState;
            return this;
        }

        /// <summary>
        /// The sort direction of the scan.
        /// </summary>
        /// <param name="scanSort"></param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions Sort(ScanSort scanSort)
        {
            SortValue = scanSort;
            return this;
        }

        /// <summary>
        /// A <see cref="CancellationToken"/> for cooperative cancellation.
        /// </summary>
        /// <param name="token"></param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions Token(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        /// <summary>
        /// Sets the Item Limit per batch. This will be applied to each stream individually, and acts as a
        /// target the server aims to reach.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions ItemLimit(uint limit)
        {
            BatchItemLimit = limit;
            return this;
        }

        /// <summary>
        /// Sets the Byte Limit per batch. This will be applied to each stream individually, and acts as a
        /// target the server aims to reach.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions ByteLimit(uint limit)
        {
            BatchByteLimit = limit;
            return this;
        }

        /// <summary>
        /// Sets the Time Limit in milliseconds for the scan to keep returning documents. This will be applied to each stream individually.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns>A <see cref="ScanOptions"/> instance for chaining.</returns>
        public ScanOptions TimeLimit(uint limit)
        {
            BatchTimeLimit = limit;
            return this;
        }

        #endregion
    }
}
