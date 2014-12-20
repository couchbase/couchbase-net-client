using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public class Services
    {
        [JsonProperty("mgmt")]
        public int Mgmt { get; set; }

        [JsonProperty("moxi")]
        public int Moxi { get; set; }

        [JsonProperty("kv")]
        public int KV { get; set; }

        [JsonProperty("capi")]
        public int Capi { get; set; }

        [JsonProperty("kvSSL")]
        public int KvSSL { get; set; }

        [JsonProperty("capiSSL")]
        public int CapiSSL { get; set; }

        [JsonProperty("mgmtSSL")]
        public int MgmtSSL { get; set; }
    }
}
