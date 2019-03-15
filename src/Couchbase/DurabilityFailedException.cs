using System;

namespace Couchbase
{
    /// <summary>
    /// A generic error raised when durability fails.
    /// </summary>
    public class DurabilityFailedException : CouchbaseException
    {
        public  DurabilityFailedException()
        {
        }

        public DurabilityFailedException(string message)
            : base(message)
        {
        }

        public DurabilityFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public Exception Reason => InnerException;
    }
}
