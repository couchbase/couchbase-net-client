using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Utils;

namespace Couchbase.Search
{
    /// <summary>
    /// Exception thrown for a failed search query response if EnsureSucess is called.
    /// </summary>
    public class CouchbaseSearchResponseException : CouchbaseResponseException
    {
        /// <summary>
        /// <see cref="SearchStatus"/> returned from Couchbase.
        /// </summary>
        public SearchStatus Status { get; private set; }

        /// <summary>
        /// Errors returned from Couchbase.
        /// </summary>
        public IReadOnlyCollection<string> Errors { get; private set; }

        /// <summary>
        /// Creates a new CouchbaseSearchResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="SearchStatus"/> returned from Couchbase.</param>
        /// <param name="errors">Errors returned from Couchbase.</param>
        public CouchbaseSearchResponseException(string message, SearchStatus status, IList<string> errors) :
            this(message, status, errors, null)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseSearchResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="SearchStatus"/> returned from Couchbase.</param>
        /// <param name="errors">Errors returned from Couchbase.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public CouchbaseSearchResponseException(string message, SearchStatus status, IList<string> errors,
            Exception innerException) :
            base(message, innerException)
        {
            Status = status;
            Errors = new ReadOnlyCollection<string>(errors ?? new string[] {});
        }

        /// <summary>
        /// Creates a new CouchbaseQueryResponseException.
        /// </summary>
        /// <param name="result">Result from Couchbase</param>
        internal static CouchbaseSearchResponseException FromResult(ISearchQueryResult result)
        {
            return new CouchbaseSearchResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Errors.FirstOrDefault(), result.Status),
                result.Status, result.Errors, result.Exception);
        }
    }
}
