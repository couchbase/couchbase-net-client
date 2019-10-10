using System;
using System.Collections.Generic;
using System.Text;

namespace RetryExample
{
    public enum RetryReason
    {
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

    public static class RetryReasonExtensions
    {
        public static bool AllowsNonIdempotentRetries(this RetryReason reason)
        {
            return !(reason == RetryReason.Unknown || reason == RetryReason.SocketClosedWhileInFlight);
        }

        public static bool AlwaysRetry(this RetryReason reason)
        {
            return reason == RetryReason.KvNotMyVBucket || reason == RetryReason.KvCollectionOutdated;
        }
    }
}
