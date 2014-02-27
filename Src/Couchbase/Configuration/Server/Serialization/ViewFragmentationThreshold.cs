using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class ViewFragmentationThreshold
    {
        [JsonProperty("percentage")]
        public int Percentage { get; set; }

        [JsonProperty("size")]
        public string Size { get; set; }
    }
}