using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    public class Metrics
    {
        [JsonProperty("elapsedTime")]
        public string ElaspedTime { get; set; }

        [JsonProperty("executionTime")]
        public string ExecutionTime { get; set; }

        [JsonProperty("resultCount")]
        public uint ResultCount { get; set; }

        [JsonProperty("resultSize")]
        public uint ResultSize { get; set; }

        [JsonProperty("mutationCount")]
        public uint MutationCount { get; set; }

        [JsonProperty("errorCount")]
        public uint ErrorCount { get; set; }

        [JsonProperty("warningCount")]
        public uint WarningCount { get; set; }
    }
}
