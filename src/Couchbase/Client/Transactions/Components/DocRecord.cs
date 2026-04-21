using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.Client.Transactions.Components
{
    internal class DocRecord
    {
        [JsonPropertyName("bkt")]
        public string BucketName { get; init; } = null!;

        [JsonPropertyName("scp")]
        public string ScopeName { get; init; } = null!;

        [JsonPropertyName("col")]
        public string CollectionName { get; init; } = null!;

        [JsonPropertyName("id")]
        public string Id { get; init; } = null!;

        [JsonConstructor]
        public DocRecord() { }

        public DocRecord(string bucketName, string scopeName, string collectionName, string id)
        {
            BucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            ScopeName = scopeName ?? throw new ArgumentNullException(nameof(scopeName));
            CollectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
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
