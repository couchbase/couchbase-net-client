using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Replication
    {
        [JsonProperty("createURI")]
        public string CreateURI { get; set; }

        [JsonProperty("validateURI")]
        public string ValidateURI { get; set; }
    }
}