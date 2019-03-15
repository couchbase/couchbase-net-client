using System;

namespace Couchbase
{
    /// <summary>
    /// A generic error raised when a key already exists for a resource.
    /// </summary>
    public class KeyExistsException : CouchbaseException
    {
        public  KeyExistsException()
        {
        }

        public KeyExistsException(string message)
            : base(message)
        {
        }

        public KeyExistsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public Exception Reason => InnerException;
    }
}
