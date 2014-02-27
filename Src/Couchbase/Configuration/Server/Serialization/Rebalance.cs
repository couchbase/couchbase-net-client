using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Rebalance
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}