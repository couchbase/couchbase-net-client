using System;
using Couchbase.IO;
using Couchbase.Utils;

namespace Couchbase.Core
{
    /// <summary>
    /// Exeception thrown for a failed key/value response if EnsureSucess is called.
    /// </summary>
    public class CouchbaseKeyValueResponseException : CouchbaseResponseException
    {
        /// <summary>
        /// <see cref="ResponseStatus"/> returned from Couchbase.
        /// </summary>
        public ResponseStatus Status { get; private set; }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="ResponseStatus"/> returned from Couchbase.</param>
        public CouchbaseKeyValueResponseException(string message, ResponseStatus status) :
            this(message, status, null)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="status"><see cref="ResponseStatus"/> returned from Couchbase.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public CouchbaseKeyValueResponseException(string message, ResponseStatus status, Exception innerException) :
            base(message, innerException)
        {
            Status = status;
        }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="result">Result from Couchbase</param>
        internal static CouchbaseKeyValueResponseException FromResult(IOperationResult result)

        {
            return new CouchbaseKeyValueResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.Status),
                result.Status, result.Exception);
        }

        /// <summary>
        /// Creates a new CouchbaseKeyValueResponseException.
        /// </summary>
        /// <param name="result">Result from Couchbase</param>
        internal static CouchbaseKeyValueResponseException FromResult(IDocumentResult result)

        {
            return new CouchbaseKeyValueResponseException(
                ExceptionUtil.GetResponseExceptionMessage(result.Message, result.Status),
                result.Status, result.Exception);
        }
    }
}
