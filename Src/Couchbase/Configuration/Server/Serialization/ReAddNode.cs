using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class ReAddNode
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}