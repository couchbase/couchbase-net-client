using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    public class Error
    {
        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("sev")]
        public Severity Severity { get; set; }

        [JsonProperty("temp")]
        public bool Temp { get; set; }
    }
}
