using Newtonsoft.Json;

namespace Couchbase.Services.Views
{
    public class View
    {
        [JsonProperty("map")]
        public string Map { get; set; }

        [JsonProperty("reduce", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Reduce { get; set; }
    }
}
