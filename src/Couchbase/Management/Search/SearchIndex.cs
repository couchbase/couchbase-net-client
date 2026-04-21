using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Couchbase.Management.Search
{
    public class SearchIndex
    {
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonProperty("sourceType")]
        [JsonPropertyName("sourceType")]
        public string SourceType { get; set; }

        [JsonProperty("sourceName")]
        [JsonPropertyName("sourceName")]
        public string SourceName { get; set; }

        [JsonProperty("uuid", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("uuid")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Uuid { get; set; }

        [JsonProperty("sourceUuid", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("sourceUuid")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SourceUuid { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("params")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, dynamic> Params { get; set; }

        [JsonProperty("sourceParams", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("sourceParams")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, dynamic> SourceParams { get; set; }

        [JsonProperty("planParams", NullValueHandling = NullValueHandling.Ignore)]
        [JsonPropertyName("planParams")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, dynamic> PlanParams { get; set; }
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
