using System;
using System.Threading;

namespace Couchbase.Services.Analytics
{
    public class IngestOptions
    {
        public IngestOptions()
        {
            IdGenerator = doc => Guid.NewGuid().ToString();
            Expiration = TimeSpan.Zero;
            Timeout = TimeSpan.FromSeconds(75);
            IngestMethod = IngestMethod.Upsert;
            CancellationToken = default;
        }

        public TimeSpan Timeout { get; set; }
        public TimeSpan Expiration { get; set; }
        public IngestMethod IngestMethod { get; set; }
        public Func<dynamic, string> IdGenerator { get; set; }
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Overrides the default Guid based key generator.
        /// </summary>
        /// <param name="idGenerator">A Func{string} that generates a valid Couchbase server key.</param>
        /// <returns></returns>
        public IngestOptions WithIdGenerator(Func<dynamic, string> idGenerator)
        {
            IdGenerator = idGenerator;
            return this;
        }

        /// <summary>
        /// The maximum time for the query to run. Overrides the default timeout of 75s.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public IngestOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        /// <summary>
        /// The lifetime of the documents ingested by Couchbase. Overrides the default of zero (0) or infinite lifespan.
        /// </summary>
        /// <param name="expiration"></param>
        /// <returns></returns>
        public IngestOptions WithExpiration(TimeSpan expiration)
        {
            Expiration = expiration;
            return this;
        }

        /// <summary>
        /// The ingest method to use when ingesting into Couchbase. Insert, Replace and Upsert are supported.
        /// </summary>
        /// <param name="ingestMethod"></param>
        /// <returns></returns>
        public IngestOptions WithIngestMethod(IngestMethod ingestMethod)
        {
            IngestMethod = ingestMethod;
            return this;
        }

        /// <summary>
        /// An optional cancellation token to use for the query.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public IngestOptions WithCancellationToken(CancellationToken token)
        {
            CancellationToken = token;
            return this;
        }
    }
}
