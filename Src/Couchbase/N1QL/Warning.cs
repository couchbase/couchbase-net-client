using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    public class Warning
    {
        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }
    }
}
