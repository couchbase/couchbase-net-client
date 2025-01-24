#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;

namespace Couchbase.Client.Transactions.DataAccess
{
    internal abstract class IAtrRepository
    {
        public readonly string AtrId;
        public readonly ICouchbaseCollection Collection;

        protected IAtrRepository(ICouchbaseCollection collection, string atrId)
        {
            Collection = collection;
            AtrId = atrId;
        }

        public string BucketName => Collection.Scope.Bucket.Name;

        public string ScopeName => Collection.Scope.Name;

        public string CollectionName => Collection.Name;

        public string FullPath => $"{BucketName}.{ScopeName}.{CollectionName}::{AtrId}";

        public AtrRef AtrRef => new AtrRef()
        {
            BucketName = BucketName,
            ScopeName = ScopeName,
            CollectionName = CollectionName,
            Id = AtrId
        };


        public abstract Task<ICouchbaseCollection?> GetAtrCollection(AtrRef atrRef);

        public abstract Task<string?> LookupAtrState();

        public abstract Task MutateAtrComplete();

        public abstract Task MutateAtrPending(ulong exp, DurabilityLevel documentDurability);

        public abstract Task MutateAtrCommit(IEnumerable<StagedMutation> stagedMutations);

        public abstract Task MutateAtrAborted(IEnumerable<StagedMutation> stagedMutations);

        public abstract Task MutateAtrRolledBack();

        public abstract Task<AtrEntry?> FindEntryForTransaction(ICouchbaseCollection atrCollection, string atrId, string? attemptId = null);

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
