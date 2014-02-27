using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class AutoCompactionSettings
    {
        [JsonProperty("parallelDBAndViewCompaction")]
        public bool ParallelDBAndViewCompaction { get; set; }

        [JsonProperty("databaseFragmentationThreshold")]
        public DatabaseFragmentationThreshold DatabaseFragmentationThreshold { get; set; }

        [JsonProperty("viewFragmentationThreshold")]
        public ViewFragmentationThreshold ViewFragmentationThreshold { get; set; }
    }
}