using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public sealed class Pools
    {
        [JsonProperty("storageTotals")]
        public StorageTotals StorageTotals { get; set; }

        [JsonProperty("serverGroupsUri")]
        public string ServerGroupsUri { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("alerts")]
        public object[] Alerts { get; set; }

        [JsonProperty("alertsSilenceURL")]
// ReSharper disable once InconsistentNaming
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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion