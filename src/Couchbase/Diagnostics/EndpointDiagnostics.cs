using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#nullable enable

namespace Couchbase.Diagnostics
{
    internal class EndpointDiagnostics : IEndpointDiagnostics
    {
        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public ServiceType Type { get; internal set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("id")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; internal set; }

        [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
        [JsonPropertyName("state")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ServiceState? State { get; internal set; }

        [JsonProperty("endpoint_state", NullValueHandling = NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
        [JsonPropertyName("endpoint_state")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EndpointState? EndpointState { get; internal set; }

        [JsonProperty("local", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("local")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Local { get; internal set; }

        [JsonProperty("remote", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("remote")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Remote { get; internal set; }

        [JsonProperty("last_activity_us", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("last_activity_us")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? LastActivity { get; internal set; }

        [JsonProperty("latency_us", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("latency_us")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Latency { get; internal set; }

        [JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("scope")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Scope { get; internal set; }

        public override string ToString() =>
            System.Text.Json.JsonSerializer.Serialize(this, DiagnoticsSerializerContext.Default.IEndpointDiagnostics);
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
