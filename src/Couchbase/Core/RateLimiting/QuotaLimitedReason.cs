using Couchbase.Core.Compatibility;

namespace Couchbase.Core.RateLimiting
{
    [InterfaceStability(Level.Uncommitted)]
    public enum QuotaLimitedReason
    {
        MaximumNumberOfCollectionsReached,
        MaximumNumberOfIndexesReached,
        ScopeSizeLimitExceeded
    }
}
