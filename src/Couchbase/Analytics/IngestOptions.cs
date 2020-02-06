using System;
using System.Threading;

using IngestMethodCls = Couchbase.Analytics.IngestMethod;

#nullable enable

namespace Couchbase.Analytics
{
    public class IngestOptions
    {
        public IngestOptions()
        {
            IdGeneratorValue = doc => Guid.NewGuid().ToString();
            ExpiryValue = TimeSpan.Zero;
            TimeoutValue = TimeSpan.FromSeconds(75);
            IngestMethodValue = IngestMethodCls.Upsert;
            TokenValue = default;
        }

        internal TimeSpan TimeoutValue { get; set; }
        internal TimeSpan ExpiryValue { get; set; }
        internal IngestMethod IngestMethodValue { get; set; }
        internal Func<dynamic, string> IdGeneratorValue { get; set; }
        internal CancellationToken TokenValue { get; set; }

        /// <summary>
        /// Overrides the default Guid based key generator.
        /// </summary>
        /// <param name="idGenerator">A Func{string} that generates a valid Couchbase server key.</param>
        /// <returns></returns>
        public IngestOptions IdGenerator(Func<dynamic, string> idGenerator)
        {
            IdGeneratorValue = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
            return this;
        }

        /// <summary>
        /// The maximum time for the query to run. Overrides the default timeout of 75s.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public IngestOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// The lifetime of the documents ingested by Couchbase. Overrides the default of zero (0) or infinite lifespan.
        /// </summary>
        /// <param name="expiry"></param>
        /// <returns></returns>
        public IngestOptions Expiry(TimeSpan expiry)
        {
            ExpiryValue = expiry;
            return this;
        }

        /// <summary>
        /// The ingest method to use when ingesting into Couchbase. Insert, Replace and Upsert are supported.
        /// </summary>
        /// <param name="ingestMethod"></param>
        /// <returns></returns>
        public IngestOptions IngestMethod(IngestMethod ingestMethod)
        {
            IngestMethodValue = ingestMethod;
            return this;
        }

        /// <summary>
        /// An optional cancellation token to use for the query.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public IngestOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }
    }
}
