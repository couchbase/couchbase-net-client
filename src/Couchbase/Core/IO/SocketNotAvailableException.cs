using System;
using Couchbase.Core.Retry;

namespace Couchbase.Core.IO
{
    public class SocketNotAvailableException : CouchbaseException, IRetryable
    {
        public SocketNotAvailableException()
        {
        }

        public SocketNotAvailableException(string message)
            : base(message)
        {
        }

        public SocketNotAvailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
