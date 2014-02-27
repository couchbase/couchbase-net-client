using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class InterestingStats
    {
        [JsonProperty("couch_docs_actual_disk_size")]
        public int CouchDocsActualDiskSize { get; set; }

        [JsonProperty("couch_docs_data_size")]
        public int CouchDocsDataSize { get; set; }

        [JsonProperty("couch_views_actual_disk_size")]
        public int CouchViewsActualDiskSize { get; set; }

        [JsonProperty("couch_views_data_size")]
        public int CouchViewsDataSize { get; set; }

        [JsonProperty("curr_items")]
        public int CurrItems { get; set; }

        [JsonProperty("curr_items_tot")]
        public int CurrItemsTot { get; set; }

        [JsonProperty("mem_used")]
        public int MemUsed { get; set; }

        [JsonProperty("vb_replica_curr_items")]
        public int VbReplicaCurrItems { get; set; }
    }
}