using System;

namespace Couchbase
{
    /// <summary>
    /// A generic error raised when a path exists for a given resource.
    /// </summary>
    public class TempFailException : CouchbaseException
    {
        public  TempFailException()
        {
        }

        public TempFailException(string message)
            : base(message)
        {
        }

        public TempFailException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public Exception Reason => InnerException;
    }
}
