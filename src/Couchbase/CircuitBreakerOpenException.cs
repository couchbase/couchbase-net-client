using System;
using Couchbase.Core.Retry;

namespace Couchbase
{
    public class CircuitBreakerOpenException : CouchbaseException, IRetryable
    {
        public CircuitBreakerOpenException()
        {
        }

        public CircuitBreakerOpenException(string message)
            : base(message)
        {
        }

        public CircuitBreakerOpenException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
