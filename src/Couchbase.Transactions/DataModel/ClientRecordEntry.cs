using System;
using Couchbase.Transactions.Components;
using Newtonsoft.Json;

namespace Couchbase.Transactions.DataModel
{
    /// <summary>
    /// A model class for JSON serialization/deserialization of the individual client record entries.
    /// </summary>
    internal record ClientRecordEntry
    {
        private const string FIELD_HEARTBEAT = "heartbeat_ms";
        private const string FIELD_EXPIRES = "expires_ms";
        private const string FIELD_NUM_ATRS = "num_atrs";

        public static string PathForEntry(string clientUuid) => $"{ClientRecordsIndex.FIELD_CLIENTS_FULL}.{clientUuid}";
        public static string PathForHeartbeat(string clientUuid) => $"{PathForEntry(clientUuid)}.{FIELD_HEARTBEAT}";
        public static string PathForExpires(string clientUuid) => $"{PathForEntry(clientUuid)}.{FIELD_EXPIRES}";
        public static string PathForNumAtrs(string clientUuid) => $"{PathForEntry(clientUuid)}.{FIELD_NUM_ATRS}";

        [JsonProperty(FIELD_HEARTBEAT)]
        public string HeartbeatMutationCas { get; set; } = "${Mutation.CAS}";

        [JsonProperty(FIELD_EXPIRES)]
        public long ExpiresMilliseconds { get; set; }

        [JsonIgnore]
        public DateTimeOffset? ParsedMutationCas => AtrEntry.ParseMutationCasFieldNoThrow(HeartbeatMutationCas);

        [JsonIgnore]
        public DateTimeOffset? Expires => ParsedMutationCas?.AddMilliseconds(ExpiresMilliseconds);

        [JsonProperty(FIELD_NUM_ATRS)]
        public int NumAtrs { get; set; }
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
