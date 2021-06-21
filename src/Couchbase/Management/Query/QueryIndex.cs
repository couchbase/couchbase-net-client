using System.Collections.Generic;
using Couchbase.Management.Views;
using Newtonsoft.Json;

namespace Couchbase.Management.Query
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

        [JsonProperty("partition")]
        public string Partition { get; set; }

        [JsonProperty("condition")]
        public string Condition { get; set; }

        [JsonProperty("index_key")]
        public List<string> IndexKey { get; set; }
    }
}
