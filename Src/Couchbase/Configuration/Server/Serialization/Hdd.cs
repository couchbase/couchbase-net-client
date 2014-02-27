using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Hdd
    {
        [JsonProperty("total")]
        public long Total { get; set; }

        [JsonProperty("quotaTotal")]
        public long QuotaTotal { get; set; }

        [JsonProperty("used")]
        public long Used { get; set; }

        [JsonProperty("usedByData")]
        public int UsedByData { get; set; }

        [JsonProperty("free")]
        public long Free { get; set; }
    }
}