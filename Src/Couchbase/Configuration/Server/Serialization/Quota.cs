using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Quota
    {
        [JsonProperty("ram")]
        public int Ram { get; set; }

        [JsonProperty("rawRAM")]
        public int RawRAM { get; set; }
    }
}