using Newtonsoft.Json;

namespace Couchbase.Management
{
    public class QueryIndex
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("is_primary")]
        public bool IsPrimary { get; set; }

        [JsonProperty("type")]
        public IndexType Type { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("keyspace_id")]
        public string Keyspace { get; set; }
    }
}