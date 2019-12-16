namespace Couchbase.Core.CircuitBreakers
{
    public enum CircuitBreakerState
    {
        Open,
        HalfOpen,
        Closed,
        Disabled
    }
}
