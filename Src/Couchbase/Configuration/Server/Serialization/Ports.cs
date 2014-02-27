using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Ports
    {
        [JsonProperty("proxy")]
        public int Proxy { get; set; }

        [JsonProperty("direct")]
        public int Direct { get; set; }
    }
}