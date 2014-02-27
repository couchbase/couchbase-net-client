using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Counters
    {
        [JsonProperty("rebalance_success")]
        public int RebalanceSuccess { get; set; }

        [JsonProperty("rebalance_start")]
        public int RebalanceStart { get; set; }
    }
}