using System;

namespace Couchbase
{
    /// <summary>
    /// Base calls for exception thrown for a failed response if EnsureSuccess is called.
    /// </summary>
    public class CouchbaseResponseException : Exception
    {
        /// <summary>
        /// Creates a new CouchbaseResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        public CouchbaseResponseException(string message) :
            base(message)
        {
        }

        /// <summary>
        /// Creates a new CouchbaseResponseException.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="innerException">Exception included in the response from Couchbase.</param>
        public CouchbaseResponseException(string message, Exception innerException) :
            base(message, innerException)
        {
        }
    }
}
