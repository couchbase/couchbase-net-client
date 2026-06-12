using Couchbase.Core.Compatibility;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Couchbase.Query
{
    public class Reason
    {
        [JsonProperty("caller")]
        [JsonPropertyName("caller")]
        public string Caller { get; set; }

        [JsonProperty("code")]
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonProperty("key")]
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonProperty("message")]
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    public class Error
    {
        [JsonProperty("msg")]
        [JsonPropertyName("msg")]
        public string Message { get; set; }

        [JsonProperty("code")]
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonProperty("sev")]
        [JsonPropertyName("sev")]
        public Severity Severity { get; set; }

        [JsonProperty("temp")]
        [JsonPropertyName("temp")]
        public bool Temp { get; set; }

        [JsonProperty("reason")]
        [JsonPropertyName("reason")]
        public Reason Reason { get; set; }

        [JsonProperty("retry")]
        [JsonPropertyName("retry")]
        public bool Retry { get; set; }

        // The transaction-protocol "cause" (retry/rollback/raise hints). Typed so it is populated by
        // whichever serializer the cluster uses, rather than read out of AdditionalData as a
        // serializer-specific node (JObject vs JsonElement) - the coupling that regressed in NCBC-4036.
        [InterfaceStability(Level.Volatile)]
        [JsonProperty("cause")]
        [JsonPropertyName("cause")]
        public QueryErrorCause Cause { get; set; }

        [InterfaceStability(Level.Volatile)]
        [Newtonsoft.Json.JsonExtensionData]
        [System.Text.Json.Serialization.JsonExtensionData]
        public IDictionary<string, object> AdditionalData { get; set; }
    }

    internal class ErrorData
    {
        public string msg { get; set; }
        public int code { get; set; }
        public string name { get; set; }
        public Severity sev { get; set; }
        public bool temp { get; set; }
        public Reason reason { get; set; }
        public bool retry { get; set; }

        internal Error ToError()
        {
            return new Error
            {
                Message = msg,
                Code = code,
                Name = name,
                Severity = sev,
                Temp = temp,
                Reason = reason,
                Retry = retry
            };
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
