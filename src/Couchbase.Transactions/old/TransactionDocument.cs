using Couchbase.Core;

namespace Couchbase.Transactions.old
{
    internal class TransactionDocument<T> : ITransactionDocument<T>, ITransactionDocumentWithBucket
    {
        public IBucket Bucket { get; }
        public string BucketName => Bucket.Name;

        public string Key { get; }
        public ulong Cas { get; set; }
        public T Content { get; }
        public TransactionDocumentStatus Status { get; set; }
        public TransactionLinks<T> Links { get; }

        public TransactionDocument(IBucket bucket, string key, ulong cas, T content, TransactionDocumentStatus status, TransactionLinks<T> links = null)
        {
            Bucket = bucket;
            Key = key;
            Cas = cas;
            Content = content;
            Status = status;
            Links = links;
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
