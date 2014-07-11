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