using System;
using System.Net.Sockets;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.KeyValue;

namespace Couchbase.Core.Retry
{
    public static class ExceptionExtensions
    {
        public static RetryReason ResolveRetryReason(this Exception e)
        {
            switch (e)
            {
                case NotMyVBucketException _: return RetryReason.KvNotMyVBucket;
                case DocumentLockedException _: return RetryReason.KvLocked;
                case TemporaryFailureException _: return RetryReason.KvTemporaryFailure;
                //case SocketNotAvailableException _: return RetryReason.SocketNotAvailable;
                case SocketException _: return RetryReason.SocketClosedWhileInFlight;
                case DurableWriteInProgressException _: return RetryReason.KvSyncWriteInProgress;
                case DurableWriteReCommitInProgressException _: return RetryReason.KvSyncWriteReCommitInProgress;
                case ServiceNotAvailableException _: return RetryReason.ServiceNotAvailable;
                case NodeNotAvailableException _:return RetryReason.NodeNotAvailable;
                case KvErrorMapRetryException _: return RetryReason.KvErrorMapRetryIndicated;
                //case ServiceResponseRetryException _: return RetryReason.ServiceResponseCodeIndicated;
                default: return RetryReason.NoRetry;
            }
        }
    }
}
