using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Transactions.Components;
using Couchbase.Transactions.DataModel;
using Couchbase.Transactions.Internal;
using Newtonsoft.Json.Linq;

namespace Couchbase.Transactions
{
    public class TransactionGetResult
    {
        private readonly IContentAsWrapper _content;

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

        public string Id { get; }
        internal string FullyQualifiedId { get; }
        public ulong Cas { get; internal set; }
        public DocumentMetadata? DocumentMetadata { get; }
        public ICouchbaseCollection Collection { get; }

        internal JObject? TxnMeta { get; set; } = null;

        public T ContentAs<T>() => _content.ContentAs<T>();

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

        internal static TransactionGetResult FromNonTransactionDoc(ICouchbaseCollection collection, string id, IContentAsWrapper content, ulong cas, DocumentMetadata documentMetadata, bool isDeleted, TransactionXattrs? transactionXattrs)
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

        internal static TransactionGetResult FromStaged(ICouchbaseCollection collection, string id, IContentAsWrapper? stagedContent, ulong cas, DocumentMetadata documentMetadata, TransactionXattrs? txn, bool isTombstone)
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
