using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Stats
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("directoryURI")]
        public string DirectoryURI { get; set; }

        [JsonProperty("nodeStatsListURI")]
        public string NodeStatsListURI { get; set; }
    }
}