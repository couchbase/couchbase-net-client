using System;

namespace Couchbase
{
    /// <summary>
    /// A generic error raised when the document body exceeds the size limit (20mb) suported by Couchbase Server.
    /// </summary>
    public class ValueTooLargeException : CouchbaseException
    {
        public  ValueTooLargeException()
        {
        }

        public ValueTooLargeException(string message)
            : base(message)
        {
        }

        public ValueTooLargeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public Exception Reason => InnerException;
    }
}
