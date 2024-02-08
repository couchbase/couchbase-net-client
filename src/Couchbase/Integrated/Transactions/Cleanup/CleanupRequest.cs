#if NET5_0_OR_GREATER
#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Couchbase.Integrated.Transactions.Components;
using Couchbase.Integrated.Transactions.DataModel;
using Couchbase.Integrated.Transactions.Support;
using Couchbase.KeyValue;
using Newtonsoft.Json.Linq;

namespace Couchbase.Integrated.Transactions.Cleanup
{
    internal record CleanupRequest(
            string AttemptId,
            string AtrId,
            ICouchbaseCollection AtrCollection,
            List<DocRecord> InsertedIds,
            List<DocRecord> ReplacedIds,
            List<DocRecord> RemovedIds,
            AttemptStates State,
            DateTimeOffset WhenReadyToBeProcessed,
            ConcurrentQueue<Exception> ProcessingErrors,
            JObject? ForwardCompatibility = null,
            string? DurabilityLevel = null)
    {
        public Couchbase.KeyValue.DurabilityLevel GetDurabilityLevel() =>
           ShortStringDurabilityLevel.FromString(this.DurabilityLevel)?.Value ?? Couchbase.KeyValue.DurabilityLevel.Majority;
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
#endif
