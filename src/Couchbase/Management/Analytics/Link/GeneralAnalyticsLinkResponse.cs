using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Couchbase.Management.Analytics.Link
{
    /// <summary>
    /// An <see cref="AnalyticsLink"/> as serialized from JSON when we don't support the type directly.
    /// </summary>
    public record GeneralAnalyticsLinkResponse(
        [JsonProperty("name")]
        string Name,

        [JsonProperty("scope")]
        string Dataverse) : AnalyticsLink(Name, Dataverse)
    {
        [JsonProperty("type")]
        [JsonPropertyName("type")]
        protected string Type { get; init; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public override string LinkType => Type;

        // Known issue: when using STJ as the cluster serializer, ExtraData is always null.
        // STJ requires Dictionary<string, JsonElement> for [JsonExtensionData], but changing
        // the type here would be a breaking API change. This will be fixed in the SDK-wide
        // Newtonsoft removal.
        [Newtonsoft.Json.JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
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
