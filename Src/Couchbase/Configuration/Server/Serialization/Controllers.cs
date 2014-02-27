using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Controllers
    {
        [JsonProperty("compactAll")]
        public string CompactAll { get; set; }

        [JsonProperty("compactDB")]
        public string CompactDB { get; set; }

        [JsonProperty("purgeDeletes")]
        public string PurgeDeletes { get; set; }

        [JsonProperty("startRecovery")]
        public string StartRecovery { get; set; }
    }
}