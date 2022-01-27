using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.DataModel;
using Couchbase.Transactions.Internal;
using Moq;

namespace Couchbase.Transactions.Tests.UnitTests.Mocks
{
    internal class MockDocumentRepository : IDocumentRepository
    {
        private long _rollingCas = 10;

        public Dictionary<string, DocumentLookupResult> Docs { get; } = new Dictionary<string, DocumentLookupResult>();
        public void Add(ICouchbaseCollection collection, string docId, DocumentLookupResult doc) => Docs.Add(collection.GetKey(docId), doc);

        // intentionally *not* the scheme CB server actually uses, because we want it to break if the code is using "_default._default" as a collection name
        // rather than splitting it up properly among Collection/Scope/Bucket.

        public Task ClearTransactionMetadata(ICouchbaseCollection collection, string docId, ulong cas, bool isDelted)
        {
            if (Docs.TryGetValue(collection.GetKey(docId), out var doc))
            {
                Interlocked.Increment(ref _rollingCas);
                doc.TransactionXattrs = null;
            }
            else
            {
                throw new DocumentNotFoundException();
            }

            return Task.CompletedTask;
        }

        public Task<DocumentLookupResult> LookupDocumentAsync(ICouchbaseCollection collection, string docId, bool fullDocument = true)
        {
            if (Docs.TryGetValue(collection.GetKey(docId), out var doc))
            {
                return Task.FromResult(doc);
            }
            else
            {
                throw new DocumentNotFoundException();
            }
        }

        public Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedInsert(ICouchbaseCollection collection, string docId, object content, IAtrRepository atr, ulong? cas = null)
        {
            var mockLookupInResult = new Mock<ILookupInResult>(MockBehavior.Strict);
            mockLookupInResult.SetupGet(l => l.IsDeleted).Returns(false);
            mockLookupInResult.SetupGet(l => l.Cas).Returns(5);

            Add(collection, docId, new DocumentLookupResult(docId, null, new JObjectContentWrapper(content), mockLookupInResult.Object, new Components.DocumentMetadata(), collection));
            Interlocked.Increment(ref _rollingCas);
            return Task.FromResult(((ulong)_rollingCas, new MutationToken("fake", 1, 2, _rollingCas)));
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedRemove(TransactionGetResult doc, IAtrRepository atr)
        {
            _= await LookupDocumentAsync(doc.Collection, doc.Id);
            Interlocked.Increment(ref _rollingCas);
            return ((ulong)_rollingCas, new MutationToken("fake", 1, 2, _rollingCas));
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedReplace(TransactionGetResult doc, object content, IAtrRepository atr, bool accessDeleted)
        {
            _ = await LookupDocumentAsync(doc.Collection, doc.Id);
            Interlocked.Increment(ref _rollingCas);
            return ((ulong)_rollingCas, new MutationToken("fake", 1, 2, _rollingCas));
        }

        public Task<(ulong updatedCas, MutationToken mutationToken)> RemoveStagedInsert(TransactionGetResult doc)
        {
            throw new NotImplementedException();
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> UnstageInsertOrReplace(ICouchbaseCollection collection, string docId, ulong cas, object finalDoc, bool insertMode)
        {
            _ = await LookupDocumentAsync(collection, docId);
            Interlocked.Increment(ref _rollingCas);
            return ((ulong)_rollingCas, new MutationToken("fake", 1, 2, _rollingCas));
        }

        public async Task UnstageRemove(ICouchbaseCollection collection, string docId, ulong cas = 0) => _ = await LookupDocumentAsync(collection, docId);
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
