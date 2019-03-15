using System;

namespace Couchbase
{
    /// <summary>
    /// A generic error raised when invalid arguments are supplied in a request to a service.
    /// </summary>
    public class InvalidArgumentException : CouchbaseException
    {
        public InvalidArgumentException()
        {
        }

        public InvalidArgumentException(string message)
            : base(message)
        {
        }

        public InvalidArgumentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public Exception Reason => InnerException;
    }
}
