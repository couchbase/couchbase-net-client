using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class BasicStats
    {
        [JsonProperty("quotaPercentUsed")]
        public double QuotaPercentUsed { get; set; }

        [JsonProperty("opsPerSec")]
        public double OpsPerSec { get; set; }

        [JsonProperty("viewOps")]
        public double ViewOps { get; set; }

        [JsonProperty("diskFetches")]
        public double DiskFetches { get; set; }

        [JsonProperty("itemCount")]
        public int ItemCount { get; set; }

        [JsonProperty("diskUsed")]
        public int DiskUsed { get; set; }

        [JsonProperty("dataUsed")]
        public int DataUsed { get; set; }

        [JsonProperty("memUsed")]
        public int MemUsed { get; set; }

        [JsonProperty("hitRatio")]
        public int? HitRatio { get; set; }
    }
}