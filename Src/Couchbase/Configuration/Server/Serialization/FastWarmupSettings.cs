using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class FastWarmupSettings
    {
        [JsonProperty("fastWarmupEnabled")]
        public bool FastWarmupEnabled { get; set; }

        [JsonProperty("minMemoryThreshold")]
        public int MinMemoryThreshold { get; set; }

        [JsonProperty("minItemsThreshold")]
        public int MinItemsThreshold { get; set; }
    }
}