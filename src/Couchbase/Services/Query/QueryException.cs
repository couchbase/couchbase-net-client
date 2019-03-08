using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Couchbase.Services.Query
{
    public class QueryException : Exception
    {
        /// <summary>
        /// <see cref="QueryStatus"/> returned from Couchbase.
        /// </summary>
        public QueryStatus Status { get; private set; }

        /// <summary>
        /// Errors returned from Couchbase.
        /// </summary>
        public IReadOnlyCollection<Error> Errors { get; private set; }

        /// <summary>
        /// Query metrics for the query.
        /// </summary>
        public Metrics Metrics { get; private set; }

        /// <summary>
        /// Creates a new CouchbaseQueryResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="QueryStatus"/> returned from Couchbase.</param>
        /// <param name="errors">List of errors returned from Couchbase.</param>
        public QueryException(string message, QueryStatus status, IList<Error> errors) :
            this(message, status, errors, null)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseQueryResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="QueryStatus"/> returned from Couchbase.</param>
        /// <param name="errors">List of errors returned from Couchbase.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public QueryException(string message, QueryStatus status, IList<Error> errors,
            Exception innerException) :
            base(message, innerException)
        {
            Status = status;
            Errors = new ReadOnlyCollection<Error>(errors ?? new Error[] {});
        }
    }
}
