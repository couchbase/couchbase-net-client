using Couchbase.Core.Compatibility;

namespace Couchbase.Core.RateLimiting
{
    [InterfaceStability(Level.Uncommitted)]
    public class RateLimitedException : CouchbaseException
    {
        public RateLimitedReason Reason { get; }

        public RateLimitedException(RateLimitedReason reason, IErrorContext ctx)
            : base(ctx, "Operation failed due to reaching a rate limit. See the error context for further details.")
        {
            Reason = reason;
        }
    }
}
