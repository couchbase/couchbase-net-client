using Couchbase.Core.Compatibility;

namespace Couchbase.Core.RateLimiting
{
    [InterfaceStability(Level.Uncommitted)]
    public enum RateLimitedReason
    {
        NetworkIngressRateLimitReached,
        NetworkEgressRateLimitReached,
        MaximumConnectionsReached,
        RequestRateLimitReached,
        ConcurrentRequestLimitReached
    }
}
