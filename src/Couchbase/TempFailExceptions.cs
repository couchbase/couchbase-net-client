using System;
using Couchbase.Core.Retry;

namespace Couchbase
{
    /// <summary>
    /// An error returned by the server when it temporarily cannot process an operation.
    /// </summary>
    public class TempFailException : CouchbaseException, IRetryable
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
