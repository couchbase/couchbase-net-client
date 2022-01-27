using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Transactions.LogUtil;
using Newtonsoft.Json;

namespace Couchbase.Transactions.Components
{
    internal class DocRecord
    {
        [JsonProperty("bkt")]
        public string BucketName { get; }

        [JsonProperty("scp")]
        public string ScopeName { get; }

        [JsonProperty("col")]
        public string CollectionName { get; }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonConstructor]
        public DocRecord(string bkt, string scp, string col, string id)
        {
            BucketName = bkt ?? throw new ArgumentNullException(nameof(bkt));
            ScopeName = scp ?? throw new ArgumentNullException(nameof(scp));
            CollectionName = col ?? throw new ArgumentNullException(nameof(col));
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public async Task<ICouchbaseCollection> GetCollection(ICluster cluster)
        {
            var bucket = await cluster.BucketAsync(BucketName).CAF();
            var scope = bucket.Scope(ScopeName);
            return scope.Collection(CollectionName);
        }
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
