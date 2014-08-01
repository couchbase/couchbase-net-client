using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public sealed class NodeExt
    {
        [JsonProperty("services")]
        public Services Services { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }
    }
}
