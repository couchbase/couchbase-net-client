using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class StorageTotals
    {
        [JsonProperty("ram")]
        public Ram Ram { get; set; }

        [JsonProperty("hdd")]
        public Hdd Hdd { get; set; }
    }
}