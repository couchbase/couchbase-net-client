#if NET5_0_OR_GREATER
#nullable enable
using System;
using Couchbase.Integrated.Transactions.Cleanup.LostTransactions;
using Newtonsoft.Json;

namespace Couchbase.Integrated.Transactions.DataModel
{
    /// <summary>
    /// A model class for JSON serialization/deserialization of the overrides section of the ClientRecord.
    /// </summary>
    internal record ClientRecordsOverride
    {
        public const string FIELD_OVERRIDE_ENABLED = "enabled";
        public const string FIELD_OVERRIDE_EXPIRES = "expires";

        [JsonProperty(FIELD_OVERRIDE_ENABLED)]
        public bool Enabled { get; init; }

        [JsonProperty(FIELD_OVERRIDE_EXPIRES)]
        public long ExpiresUnixNanos { get; init; }

        [JsonIgnore()]
        public DateTimeOffset Expires => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresUnixNanos / ClientRecordDetails.NanosecondsPerMillisecond);
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
