namespace Couchbase.Core.Retry
{
    public static class RetryReasonExtensions
    {
        public static bool AllowsNonIdempotentRetries(this RetryReason reason)
        {
            return !(reason == RetryReason.Unknown || reason == RetryReason.SocketClosedWhileInFlight);
        }

        public static bool AlwaysRetry(this RetryReason reason)
        {
            return reason == RetryReason.KvNotMyVBucket ||
                   reason == RetryReason.KvCollectionOutdated ||
                   reason == RetryReason.ViewsNoActivePartition ||
                   reason == RetryReason.CircuitBreakerOpen;
        }
    }
}
