using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal class Ddocs
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}