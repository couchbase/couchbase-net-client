using System.Collections.Generic;
using Newtonsoft.Json;

namespace Couchbase.Management.Search
{
    public class SearchIndex
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("sourceType")]
        public string SourceType { get; set; }

        [JsonProperty("sourceName")]
        public string SourceName { get; set; }

        [JsonProperty("uuid", NullValueHandling = NullValueHandling.Ignore)]
        public string Uuid { get; set; }

        [JsonProperty("sourceUuid", NullValueHandling = NullValueHandling.Ignore)]
        public string SourceUuid { get; set; }

        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, dynamic> Params { get; set; }

        [JsonProperty("sourceParams", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, dynamic> SourceParams { get; set; }

        [JsonProperty("planParams", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, dynamic> PlanParams { get; set; }
    }
}
