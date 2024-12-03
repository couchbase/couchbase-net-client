#nullable enable
using System;
using Couchbase.Integrated.Transactions.Components;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#pragma warning disable CS1591

namespace Couchbase.Integrated.Transactions.DataModel
{
    // TODO:  Everything in DataModel should probably be an internal record
    internal class TransactionXattrs
    {
        [JsonProperty("id")]
        public CompositeId? Id { get; set; }

        [JsonProperty("atr")]
        public AtrRef? AtrRef { get; set; }

        [JsonProperty("op")]
        public StagedOperation? Operation { get; set; }

        [JsonProperty("restore")]
        public DocumentMetadata? RestoreMetadata { get; set; }

        [JsonProperty("fc")]
        public JObject? ForwardCompatibility { get; set; }

        internal void ValidateMinimum()
        {
            if (Id?.AttemptId == null
                || Id?.TransactionId == null
                || AtrRef?.Id == null
                || AtrRef?.BucketName == null
                || AtrRef?.CollectionName == null)
            {
                throw new InvalidOperationException("Transaction metadata was in invalid state.");
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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





