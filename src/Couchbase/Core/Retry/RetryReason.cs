using Couchbase.Core.Compatibility;
using System;
using System.Text.Json.Serialization;

namespace Couchbase.Core.Retry
{
    [InterfaceStability(Level.Volatile)]
    [JsonConverter(typeof(JsonStringEnumConverter<RetryReason>))]
    public enum RetryReason
    {
        NoRetry = -1,
        Unknown,
        SocketNotAvailable,
        ServiceNotAvailable,
        NodeNotAvailable,
        KvNotMyVBucket,
        [Obsolete("Use ScopeNotFound or CollectionNotFound.")]
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
        ViewsNoActivePartition,
        SendQueueFull,
        CollectionNotFound,
        ScopeNotFound,
        QueryErrorRetryable
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
