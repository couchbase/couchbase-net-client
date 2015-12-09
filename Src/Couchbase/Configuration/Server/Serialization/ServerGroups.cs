using System.Collections.Generic;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public class ServerGroups
    {
        [JsonProperty("groups")]
        public List<Group> Groups { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}
