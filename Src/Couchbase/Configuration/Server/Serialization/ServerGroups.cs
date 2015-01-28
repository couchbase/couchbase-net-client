using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
