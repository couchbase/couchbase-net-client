namespace Couchbase.Core.Retry
{
    public enum RetryReason
    {
        NoRetry = -1,
        Unknown,
        SocketNotAvailable,
        ServiceNotAvailable,
        NodeNotAvailable,
        KvNotMyVBucket,
        KvCollectionOutdated,
        KvErrorMapRetryIndicated,
        KvLocked,
        KvTemporaryFailure,
        KvSyncWriteInProgress,
        KvSyncWriteReCommitInProgress,
        ServiceResponseCodeIndicated,
        SocketClosedWhileInFlight,
        CircuitBreakerOpen,
        QueryPreparedStatementFailure,
        QueryIndexNotFound,
        AnalyticsTemporaryFailure,
        SearchTooManyRequests,
        ViewsTemporaryFailure,
        ViewsNoActivePartition
    }
}
