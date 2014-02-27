using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Settings
    {
        [JsonProperty("maxParallelIndexers")]
        public string MaxParallelIndexers { get; set; }

        [JsonProperty("viewUpdateDaemon")]
        public string ViewUpdateDaemon { get; set; }
    }
}