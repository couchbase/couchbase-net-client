using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;

namespace Couchbase.Query
{
    /// <summary>
    /// Represents an error that occured while performing a query operation while using the Query Service.
    /// </summary>
    public class QueryException : CouchbaseException
    {
        /// <summary>
        /// Creates a new QueryException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="QueryStatus"/> returned from Couchbase.</param>
        /// <param name="errors">List of errors returned from Couchbase.</param>
        public QueryException(string message, QueryStatus status, IList<Error> errors) :
            this(message, status, errors, null)
        {
        }

        /// <summary>
        /// Creates a new QueryException.
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

        /// <summary>
        /// The HTTP status code that was returned by the Query service
        /// </summary>
        public HttpStatusCode StatusCode { get; internal set; }

        /// <summary>
        /// <see cref="QueryStatus"/> returned from Couchbase.
        /// </summary>
        public QueryStatus Status { get; internal set; }

        /// <summary>
        /// Errors returned from Couchbase.
        /// </summary>
        public IReadOnlyCollection<Error> Errors { get; private set; }

        /// <summary>
        /// Query metrics for the query.
        /// </summary>
        public Metrics Metrics { get; internal set; }
    }
}
