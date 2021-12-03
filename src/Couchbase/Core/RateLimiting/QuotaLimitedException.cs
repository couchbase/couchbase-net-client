using Couchbase.Core.Compatibility;

namespace Couchbase.Core.RateLimiting
{
    [InterfaceStability(Level.Uncommitted)]
    public class QuotaLimitedException : CouchbaseException
    {
        public QuotaLimitedException(QuotaLimitedReason reason)
        {
            Reason = reason;
        }

        public QuotaLimitedException(QuotaLimitedReason reason, string message) : base(message)
        {
            Reason = reason;
        }

        public QuotaLimitedException(QuotaLimitedReason reason, IErrorContext ctx)
            : base(ctx, "Operation failed due to reaching a rate limit. See the error context for further details.")
        {
            Reason = reason;
        }

        public QuotaLimitedReason Reason { get; private set; }
    }
}
