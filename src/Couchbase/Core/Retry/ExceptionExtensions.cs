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
                case SendQueueFullException _: return RetryReason.SendQueueFull;
                default:
                {
                    return RetryReason.NoRetry;
                }
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
