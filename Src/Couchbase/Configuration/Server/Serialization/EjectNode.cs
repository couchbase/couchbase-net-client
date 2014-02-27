using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal class EjectNode
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}