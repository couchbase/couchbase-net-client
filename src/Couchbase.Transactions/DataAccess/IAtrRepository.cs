using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Transactions.Components;
using Couchbase.Transactions.DataModel;
using Newtonsoft.Json.Linq;

namespace Couchbase.Transactions.DataAccess
{
    internal interface IAtrRepository
    {
        string AtrId { get; }

        string BucketName => Collection.Scope.Bucket.Name;

        string ScopeName => Collection.Scope.Name;

        string CollectionName => Collection.Name;

        string FullPath => $"{BucketName}.{ScopeName}.{CollectionName}::{AtrId}";

        AtrRef AtrRef => new AtrRef()
        {
            BucketName = BucketName,
            ScopeName = ScopeName,
            CollectionName = CollectionName,
            Id = AtrId
        };

        ICouchbaseCollection Collection { get; }

        Task<ICouchbaseCollection?> GetAtrCollection(AtrRef atrRef);

        Task<string> LookupAtrState();

        Task MutateAtrComplete();

        Task MutateAtrPending(ulong exp, DurabilityLevel documentDurability);

        Task MutateAtrCommit(IEnumerable<StagedMutation> stagedMutations);

        Task MutateAtrAborted(IEnumerable<StagedMutation> stagedMutations);

        Task MutateAtrRolledBack();

        Task<AtrEntry?> FindEntryForTransaction(ICouchbaseCollection atrCollection, string atrId, string? attemptId = null);

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
