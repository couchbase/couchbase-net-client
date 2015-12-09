using System.Collections.Generic;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public class Group
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("addNodeUri")]
        public string AddNodeUri { get; set; }

        [JsonProperty("nodes")]
        public List<Node> Nodes { get; set; }
    }
}
