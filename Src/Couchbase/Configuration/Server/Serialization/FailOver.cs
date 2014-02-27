using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class FailOver
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}