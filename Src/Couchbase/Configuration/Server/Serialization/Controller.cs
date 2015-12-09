using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public class Controller
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("validateURI")]
        public string ValidateUri { get; set; }

        [JsonProperty("startURI")]
        public string StartUri { get; set; }

        [JsonProperty("cancelURI")]
        public string CancelUri { get; set; }
    }
}
