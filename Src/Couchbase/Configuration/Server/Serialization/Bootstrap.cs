using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Bootstrap
    {
        [JsonProperty("pools")]
        public Pool[] Pools { get; set; }

        [JsonProperty("isAdminCreds")]
        public bool IsAdminCreds { get; set; }

        [JsonProperty("settings")]
        public Settings Settings { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("implementationVersion")]
        public string ImplementationVersion { get; set; }

        [JsonProperty("componentsVersion")]
        public ComponentsVersion ComponentsVersion { get; set; }
    }
}