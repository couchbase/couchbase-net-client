namespace Couchbase.Core.Retry
{
    public enum RetryReason
    {
        NoRetry,
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
        SocketClosedWhileInFlight
    }
}
