using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Tasks
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}