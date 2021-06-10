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
        protected string Type { get; init; }

        [JsonIgnore]
        public override string LinkType => Type;

        [JsonExtensionData]
        public IDictionary<string, JToken> ExtraData { get; set; }
    }
}
