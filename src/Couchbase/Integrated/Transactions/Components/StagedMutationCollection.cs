#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.KeyValue;

namespace Couchbase.Integrated.Transactions.Components
{
    internal class StagedMutationCollection
    {
        private readonly ConcurrentDictionary<string, StagedMutation> _stagedMutations = new();
        public bool IsEmpty => _stagedMutations.IsEmpty;

        public IReadOnlyCollection<StagedMutation> ToList() => _stagedMutations.Values.ToList();

        public void Clear() => _stagedMutations.Clear();

        public IEnumerable<StagedMutation> Inserts() => ToList().Where(sm => sm.Type == StagedMutationType.Insert);
        public IEnumerable<StagedMutation> Replaces() => ToList().Where(sm => sm.Type == StagedMutationType.Replace);
        public IEnumerable<StagedMutation> Removes() => ToList().Where(sm => sm.Type == StagedMutationType.Remove);
        public bool Contains(string fullyQualifiedId) => _stagedMutations.ContainsKey(fullyQualifiedId);
        public bool Contains(ICouchbaseCollection collection, string id) => Contains(TransactionGetResult.GetFullyQualifiedId(collection, id));

        internal StagedMutation? Find(ICouchbaseCollection collection, string id)
        {
            if (_stagedMutations.TryGetValue(TransactionGetResult.GetFullyQualifiedId(collection, id), out var result))
            {
                return result;
            }

            return null;
        }

        internal StagedMutation? Find(TransactionGetResult doc) => Find(doc.Collection, doc.Id);

        internal void Add(StagedMutation stagedMutation)
        {
            _stagedMutations[stagedMutation.Doc.FullyQualifiedId] = stagedMutation;
        }
        internal void Remove(StagedMutation stagedMutation) => Remove(stagedMutation.Doc);
        internal void Remove(TransactionGetResult doc) => _stagedMutations.TryRemove(doc.FullyQualifiedId, out var _);
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







