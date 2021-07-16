using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Couchbase.Diagnostics
{
    internal class EndpointDiagnostics : IEndpointDiagnostics
    {
        [JsonIgnore]
        public ServiceType Type { get; internal set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; internal set; }

        [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ServiceState? State { get; internal set; }

        [JsonProperty("local", NullValueHandling = NullValueHandling.Ignore)]
        public string Local { get; internal set; }

        [JsonProperty("remote", NullValueHandling = NullValueHandling.Ignore)]
        public string Remote { get; internal set; }

        [JsonProperty("last_activity_us", NullValueHandling = NullValueHandling.Ignore)]
        public long? LastActivity { get; internal set; }

        [JsonProperty("latency_us", NullValueHandling = NullValueHandling.Ignore)]
        public long? Latency { get; internal set; }

        [JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)]
        public string Scope { get; internal set; }

        public void SetLatency(long latency)
        {
            Latency = latency / 10;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
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
