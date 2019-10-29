using System;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.KeyValue;

namespace Couchbase.Core.Retry
{
    public static class ExceptionExtensions
    {
        public static RetryReason ResolveRetryReason(this CouchbaseException e)
        {
            switch (e)
            {
                case NotMyVBucketException _: return RetryReason.KvNotMyVBucket;
                case DocumentLockedException _: return RetryReason.KvLocked;
                case TemporaryFailureException _: return RetryReason.KvTemporaryFailure;
                //case SocketNotAvailableException _: return RetryReason.SocketNotAvailable;
                //case SocketException _: return RetryReason.SocketClosedWhileInFlight;
                case DurableWriteInProgressException _: return RetryReason.KvSyncWriteInProgress;
                case DurableWriteReCommitInProgressException _: return RetryReason.KvSyncWriteReCommitInProgress;
                case ServiceNotAvailableException _: return RetryReason.ServiceNotAvailable;
                case NodeNotAvailableException _:return RetryReason.NodeNotAvailable;
                case KvErrorMapRetryException _: return RetryReason.KvErrorMapRetryIndicated;
                //case ServiceResponseRetryException _: return RetryReason.ServiceResponseCodeIndicated;
                case PreparedStatementException _: return RetryReason.QueryPreparedStatementFailure;
                case IndexFailureException _: return RetryReason.QueryIndexNotFound;
                default:
                {
                    return RetryReason.NoRetry;
                }
            }
        }
    }
}
