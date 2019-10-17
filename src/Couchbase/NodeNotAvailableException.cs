using System;
using Couchbase.Core.Retry;

namespace Couchbase
{
    public class NodeNotAvailableException : CouchbaseException, IRetryable
    {
        public NodeNotAvailableException()
        {
        }

        public NodeNotAvailableException(string message)
            : base(message)
        {
        }

        public NodeNotAvailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
