#nullable enable
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.DataModel;

namespace Couchbase.Client.Transactions.DataAccess
{
    internal interface IDocumentRepository
    {
        Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedInsert(ICouchbaseCollection collection, string docId, object content, string opId, IAtrRepository atr, ulong? cas = null);
        Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedReplace(TransactionGetResult doc, object content, string opId, IAtrRepository atr, bool accessDeleted);
        Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedRemove(TransactionGetResult doc, IAtrRepository atr);
        Task<(ulong updatedCas, MutationToken mutationToken)> RemoveStagedInsert(TransactionGetResult doc);
        Task<(ulong updatedCas, MutationToken? mutationToken)> UnstageInsertOrReplace(ICouchbaseCollection collection, string docId, ulong cas, object finalDoc, bool insertMode);
        Task UnstageRemove(ICouchbaseCollection collection, string docId, ulong cas = 0);

        Task ClearTransactionMetadata(ICouchbaseCollection collection, string docId, ulong cas, bool isDeleted);

        Task<DocumentLookupResult> LookupDocumentAsync(ICouchbaseCollection collection, string docId, bool fullDocument = true);
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
