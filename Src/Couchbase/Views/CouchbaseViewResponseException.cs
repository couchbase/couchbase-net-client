using System;
using System.Net;
using Couchbase.Utils;

namespace Couchbase.Views
{
    /// <summary>
    /// Exception thrown for a failed view query response if EnsureSucess is called.
    /// </summary>
    public class CouchbaseViewResponseException : CouchbaseResponseException
    {
        /// <summary>
        /// <see cref="HttpStatusCode"/> returned from Couchbase.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Creates a new CouchbaseViewResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="HttpStatusCode"/> returned from Couchbase.</param>
        public CouchbaseViewResponseException(string message, HttpStatusCode status) :
            this(message, status, null)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseViewResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="HttpStatusCode"/> returned from Couchbase.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public CouchbaseViewResponseException(string message, HttpStatusCode status, Exception innerException) :
            base(message, innerException)
        {
            StatusCode = status;
        }

        internal static CouchbaseViewResponseException FromResult<T>(IViewResult<T> result)
        {
            return new CouchbaseViewResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.StatusCode),
                result.StatusCode, result.Exception);
        }
    }
}
