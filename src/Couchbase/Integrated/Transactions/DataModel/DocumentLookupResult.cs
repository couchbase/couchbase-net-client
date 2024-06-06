#nullable enable
using System;
using Couchbase.Integrated.Transactions.Components;
using Couchbase.Integrated.Transactions.Internal;
using Couchbase.KeyValue;

#pragma warning disable CS1591

namespace Couchbase.Integrated.Transactions.DataModel
{
    // TODO:  This should be made internal
    internal class DocumentLookupResult
    {
        internal DocumentLookupResult(
            string id,
            IContentAsWrapper? unstagedContent,
            IContentAsWrapper? stagedContent,
            ILookupInResult lookupInResult,
            DocumentMetadata? documentMetadata,
            ICouchbaseCollection documentCollection)
        {
            Id = id;
            LookupInResult = lookupInResult;
            StagedContent = stagedContent;
            UnstagedContent = unstagedContent;
            DocumentMetadata = documentMetadata;
            DocumentCollection = documentCollection;
        }

        private ILookupInResult LookupInResult { get; }

        public string Id { get; }

        public TransactionXattrs? TransactionXattrs { get; set; } = null;

        public DocumentMetadata? DocumentMetadata { get; }

        public bool IsDeleted => LookupInResult.IsDeleted;

        public ulong Cas => LookupInResult.Cas;

        internal IContentAsWrapper? UnstagedContent { get; }

        internal IContentAsWrapper? StagedContent { get; }

        internal ICouchbaseCollection DocumentCollection { get; }

        public TransactionGetResult GetPreTransactionResult() => TransactionGetResult.FromNonTransactionDoc(
                collection: DocumentCollection,
                id: Id,
                content: UnstagedContent ?? throw new ArgumentNullException(nameof(UnstagedContent)),
                cas: Cas,
                documentMetadata: DocumentMetadata,
                isDeleted: IsDeleted,
                transactionXattrs: TransactionXattrs);

        public TransactionGetResult GetPostTransactionResult() => TransactionGetResult.FromStaged(
                DocumentCollection,
                Id,
                StagedContent,
                Cas,
                DocumentMetadata,
                TransactionXattrs,
                IsDeleted
            );
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





