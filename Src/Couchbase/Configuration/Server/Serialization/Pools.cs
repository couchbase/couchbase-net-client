using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Pools
    {
        [JsonProperty("storageTotals")]
        public StorageTotals StorageTotals { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("alerts")]
        public object[] Alerts { get; set; }

        [JsonProperty("alertsSilenceURL")]
        public string AlertsSilenceURL { get; set; }

        [JsonProperty("nodes")]
        public Node[] Nodes { get; set; }

        [JsonProperty("buckets")]
        public Buckets Buckets { get; set; }

        [JsonProperty("remoteClusters")]
        public RemoteClusters RemoteClusters { get; set; }

        [JsonProperty("controllers")]
        public Controllers Controllers { get; set; }

        [JsonProperty("balanced")]
        public bool Balanced { get; set; }

        [JsonProperty("failoverWarnings")]
        public object[] FailoverWarnings { get; set; }

        [JsonProperty("rebalanceStatus")]
        public string RebalanceStatus { get; set; }

        [JsonProperty("rebalanceProgressUri")]
        public string RebalanceProgressUri { get; set; }

        [JsonProperty("stopRebalanceUri")]
        public string StopRebalanceUri { get; set; }

        [JsonProperty("nodeStatusesUri")]
        public string NodeStatusesUri { get; set; }

        [JsonProperty("maxBucketCount")]
        public int MaxBucketCount { get; set; }

        [JsonProperty("autoCompactionSettings")]
        public AutoCompactionSettings AutoCompactionSettings { get; set; }

        [JsonProperty("fastWarmupSettings")]
        public FastWarmupSettings FastWarmupSettings { get; set; }

        [JsonProperty("tasks")]
        public Tasks Tasks { get; set; }

        [JsonProperty("counters")]
        public Counters Counters { get; set; }

        [JsonProperty("stopRebalanceIsSafe")]
        public bool StopRebalanceIsSafe { get; set; }
    }
}