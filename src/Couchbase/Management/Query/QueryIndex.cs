using System.Collections.Generic;
using Couchbase.Management.Views;
using Newtonsoft.Json;

namespace Couchbase.Management.Query
{
    public class QueryIndex
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("is_primary")]
        public bool IsPrimary { get; set; }

        [JsonProperty("type")]
        public IndexType Type { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("keyspace_id")]
        public string Keyspace { get; set; }

        [JsonProperty("partition")]
        public string Partition { get; set; }

        [JsonProperty("condition")]
        public string Condition { get; set; }

        [JsonProperty("index_key")]
        public List<string> IndexKey { get; set; }

        [JsonProperty("scope_id")]
        public string ScopeName { get; set; }

        [JsonProperty("bucket_id")]
        internal string BucketNameField { get; set; }

        public string BucketName
        {
            get => BucketNameField ?? Keyspace;
            set => BucketNameField = value;
        }

        public string CollectionName => BucketNameField != null && ScopeName != null ? Keyspace : null;
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
