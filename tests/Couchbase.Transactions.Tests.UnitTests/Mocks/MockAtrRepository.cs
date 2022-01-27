using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Transactions.Components;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.DataModel;
using Moq;

namespace Couchbase.Transactions.Tests.UnitTests.Mocks
{
    internal class MockAtrRepository : IAtrRepository
    {
        public string AtrId { get; } = "MockATR" + Guid.NewGuid();
        public string BucketName => "MockAtrBucket";
        public string ScopeName => "MockAtrScope";
        public string CollectionName => "MockAtrCollection";

        public ICouchbaseCollection Collection { get; }

        private string FullId => Collection.GetKey(AtrId);

        public Dictionary<string, AtrEntry> Atrs = new Dictionary<string, AtrEntry>();

        public MockAtrRepository()
        {
            Collection = new MockCollectionWithNames(CollectionName, ScopeName, BucketName);
        }

        public Task<AtrEntry> FindEntryForTransaction(ICouchbaseCollection atrCollection, string atrId, string attemptId)
        {
            if (Atrs.TryGetValue(atrCollection.GetKey(atrId), out var atr))
            {
                return Task.FromResult(atr);
            }

            throw new PathNotFoundException();
        }

        public Task<ICouchbaseCollection> GetAtrCollection(AtrRef atrRef) => Task.FromResult((ICouchbaseCollection)new MockCollectionWithNames(atrRef.CollectionName, atrRef.ScopeName, atrRef.BucketName));

        public Task<string> LookupAtrState()
        {
            if (Atrs.TryGetValue(FullId, out var atr))
            {
                return Task.FromResult(atr.State.ToString());
            }

            throw new PathNotFoundException();
        }

        public Task MutateAtrAborted(IEnumerable<StagedMutation> stagedMutations) => UpdateStateOrThrow(Support.AttemptStates.ABORTED);

        public Task MutateAtrCommit(IEnumerable<StagedMutation> stagedMutations) => UpdateStateOrThrow(Support.AttemptStates.COMMITTED);

        public Task MutateAtrComplete() => UpdateStateOrThrow(Support.AttemptStates.COMPLETED);

        public Task MutateAtrPending(ulong exp, DurabilityLevel durabilityLevel)
        {
            var atrEntry = new AtrEntry()
            {
                State = Support.AttemptStates.PENDING,
                DurabilityLevel = new ShortStringDurabilityLevel(durabilityLevel).ToString()
            };

            Atrs[FullId] = atrEntry;
            return Task.CompletedTask;
        }

        public Task MutateAtrRolledBack() => UpdateStateOrThrow(Support.AttemptStates.ROLLED_BACK);

        private Task UpdateStateOrThrow(Support.AttemptStates state)
        {
            if (Atrs.TryGetValue(FullId, out var atr))
            {
                atr.State = state;
                return Task.CompletedTask;
            }

            throw new PathNotFoundException();
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
