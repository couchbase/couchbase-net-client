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
        public NodeExt()
        {
            Services = new Services
            {
                KV = (int)DefaultPorts.Direct,
                Moxi = (int)DefaultPorts.Proxy,
                KvSSL = (int)DefaultPorts.SslDirect,
                Capi = (int)DefaultPorts.CApi,
                CapiSSL = (int)DefaultPorts.HttpsCApi,
                MgmtSSL = (int)DefaultPorts.HttpsMgmt,
                Mgmt = (int)DefaultPorts.MgmtApi
            };
        }
        [JsonProperty("services")]
        public Services Services { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }
    }
}
