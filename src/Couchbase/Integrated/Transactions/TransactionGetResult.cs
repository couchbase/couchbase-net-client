#nullable enable
using Couchbase.Core.Compatibility;
using Couchbase.Integrated.Transactions.Components;
using Couchbase.Integrated.Transactions.DataModel;
using Couchbase.Integrated.Transactions.Internal;
using Couchbase.KeyValue;
using Newtonsoft.Json.Linq;

namespace Couchbase.Integrated.Transactions
{
    /// <summary>
    /// The result of a Get or GetOptional operation an a transaction context."/>
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public class TransactionGetResult
    {
        private readonly IContentAsWrapper _content;

        /// <summary>
        /// Placeholder for an empty result.
        /// </summary>
        public static readonly TransactionGetResult? Empty = null;

        private TransactionGetResult(
            string id,
            IContentAsWrapper? content,
            ulong cas,
            ICouchbaseCollection collection,
            TransactionXattrs? transactionXattrs,
            DocumentMetadata? documentMetadata,
            bool isTombstone)
        {
            Id = id;
            FullyQualifiedId = GetFullyQualifiedId(collection, id);
            _content = content ?? new JObjectContentWrapper(new { });
            Cas = cas;
            Collection = collection;
            TransactionXattrs = transactionXattrs;
            DocumentMetadata = documentMetadata;
            IsDeleted = isTombstone;
        }

        internal bool IsDeleted { get; }

        internal TransactionXattrs? TransactionXattrs { get; }

        /// <summary>
        /// Gets the ID of the document.
        /// </summary>
        public string Id { get; }
        internal string FullyQualifiedId { get; }

        /// <summary>
        /// Gets the CAS value of the document for future mutations.
        /// </summary>
        public ulong Cas { get; internal set; }

        /// <summary>
        /// Gets the document metadata.
        /// </summary>
        public DocumentMetadata? DocumentMetadata { get; }

        /// <summary>
        /// Gets the collection the document belongs to.
        /// </summary>
        public ICouchbaseCollection Collection { get; }

        /// <summary>
        /// Gets the transactional metadata of the document.
        /// </summary>
        internal JObject? TxnMeta { get; set; } = null;

        /// <summary>
        /// Deserialize the content of the document.
        /// </summary>
        /// <typeparam name="T">The type of document contained.</typeparam>
        /// <returns>A deserialized instance, or null.</returns>
        public T? ContentAs<T>() => _content.ContentAs<T>();

        internal static string GetFullyQualifiedId(ICouchbaseCollection collection, string id) =>
            $"{collection.Scope.Bucket.Name}::{collection.Scope.Name}::{collection.Name}::{id}";

        internal static TransactionGetResult FromInsert(
            ICouchbaseCollection collection,
            string id,
            IContentAsWrapper content,
            string transactionId,
            string attemptId,
            string atrId,
            string atrBucketName,
            string atrScopeName,
            string atrCollectionName,
            ulong updatedCas,
            bool isDeleted
            )
        {
            var txn = new TransactionXattrs();
            txn.AtrRef = new AtrRef()
            {
                BucketName =  atrBucketName,
                CollectionName = atrCollectionName,
                ScopeName = atrScopeName,
                Id = atrId
            };

            txn.Id = new CompositeId()
            {
                Transactionid = transactionId,
                AttemptId = attemptId
            };

            return new TransactionGetResult(
                id,
                content,
                updatedCas,
                collection,
                txn,
                null,
                isDeleted
            );
        }

        internal static TransactionGetResult FromOther(
            TransactionGetResult doc,
            IContentAsWrapper content)
        {
            // TODO: replacement for Links

            return new TransactionGetResult(
                doc.Id,
                content,
                doc.Cas,
                doc.Collection,
                doc.TransactionXattrs,
                doc.DocumentMetadata,
                doc.IsDeleted
                );
        }

        internal static TransactionGetResult FromNonTransactionDoc(ICouchbaseCollection collection, string id, IContentAsWrapper content, ulong cas, DocumentMetadata? documentMetadata, bool isDeleted, TransactionXattrs? transactionXattrs)
        {
            return new TransactionGetResult(
                id: id,
                content: content,
                cas: cas,
                collection: collection,
                transactionXattrs: transactionXattrs,
                documentMetadata: documentMetadata,
                isTombstone: isDeleted
            );
        }

        internal static TransactionGetResult FromStaged(ICouchbaseCollection collection, string id, IContentAsWrapper? stagedContent, ulong cas, DocumentMetadata? documentMetadata, TransactionXattrs? txn, bool isTombstone)
        {
            return new TransactionGetResult(
                id,
                stagedContent,
                cas,
                collection,
                txn,
                documentMetadata,
                isTombstone
                );
        }

        internal static TransactionGetResult FromQueryGet(ICouchbaseCollection collection, string id, QueryGetResult queryResult)
        {
            return new TransactionGetResult(
                id,
                new JObjectContentWrapper(queryResult.doc),
                ulong.Parse(queryResult.scas),
                collection,
                null,
                documentMetadata: null,
                isTombstone: false)
            {
                TxnMeta = queryResult.txnMeta
            };
        }

        internal static TransactionGetResult FromQueryInsert(ICouchbaseCollection collection, string id, object originalDoc, QueryInsertResult queryResult)
        {
            return new TransactionGetResult(
                id,
                new JObjectContentWrapper(originalDoc),
                ulong.Parse(queryResult.scas),
                collection,
                null,
                documentMetadata: null,
                isTombstone: false);
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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








