using Couchbase.Core.Retry;

namespace Couchbase.Core.CircuitBreakers
{
    public class CircuitBreakerException : CouchbaseException, IRetryable
    {
    }
}
