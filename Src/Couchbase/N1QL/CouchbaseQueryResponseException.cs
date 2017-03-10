using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Couchbase.Utils;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Exception thrown for a failed N1QL response if EnsureSucess is called.
    /// </summary>
    public class CouchbaseQueryResponseException : CouchbaseResponseException
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
        /// Creates a new CouchbaseQueryResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="QueryStatus"/> returned from Couchbase.</param>
        /// <param name="errors">List of errors returned from Couchbase.</param>
        public CouchbaseQueryResponseException(string message, QueryStatus status, IList<Error> errors) :
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
        public CouchbaseQueryResponseException(string message, QueryStatus status, IList<Error> errors,
            Exception innerException) :
            base(message, innerException)
        {
            Status = status;
            Errors = new ReadOnlyCollection<Error>(errors ?? new Error[] {});
        }

        /// <summary>
        /// Creates a new CouchbaseQueryResponseException.
        /// </summary>
        /// <param name="result">Result from Couchbase</param>
        internal static CouchbaseQueryResponseException FromResult<T>(IQueryResult<T> result)
        {
            return new CouchbaseQueryResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.Status),
                result.Status, result.Errors, result.Exception);
        }
    }
}
