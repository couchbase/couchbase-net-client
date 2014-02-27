using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class SystemStats
    {
        [JsonProperty("cpu_utilization_rate")]
        public double CpuUtilizationRate { get; set; }

        [JsonProperty("swap_total")]
        public int SwapTotal { get; set; }

        [JsonProperty("swap_used")]
        public int SwapUsed { get; set; }
    }
}