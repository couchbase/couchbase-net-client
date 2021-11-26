using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.DataStructures
{
    public class PersistentSet<TValue> : PersistentStoreBase<TValue>, IPersistentSet<TValue>
    {
        internal PersistentSet(ICouchbaseCollection collection, string key, ILogger? logger, IRedactor? redactor)
            : base(collection, key, logger, redactor, new object(), false)
        {
        }

        protected override void CreateBackingStore()
        {
            if (BackingStoreChecked) return;
            try
            {
                Collection.InsertAsync(Key, new HashSet<TValue>()).GetAwaiter().GetResult();
                BackingStoreChecked = true;
            }
            catch (DocumentExistsException e)
            {
                //ignore - the doc already exists for this collection
                Logger.LogTrace(e, "The PersistentList backing document already exists for ID {key}. Not an error.",
                    Redactor?.UserData(Key));
            }
        }

        protected new ISet<TValue> GetList()
        {
            return GetListAsync().GetAwaiter().GetResult();
        }

        protected new async Task<ISet<TValue>> GetListAsync()
        {
            CreateBackingStore();
            using var result = await Collection.GetAsync(Key).ConfigureAwait(false);
            return result.ContentAs<ISet<TValue>>().EnsureNotNullForDataStructures();
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return GetList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Add(TValue item)
        {
            return AddAsync(item).GetAwaiter().GetResult();
        }

        public async Task<bool> AddAsync(TValue item)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var items = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            var added = items.Add(item);
            if (added)
            {
                await Collection.UpsertAsync(Key, items).ConfigureAwait(false);
            }

            return added;
        }

        public void ExceptWith(IEnumerable<TValue> other)
        {
            ExceptWithAsync(other).GetAwaiter().GetResult();
        }

        public void IntersectWith(IEnumerable<TValue> other)
        {
            IntersectWithAsync(other).GetAwaiter().GetResult();
        }

        public bool IsProperSubsetOf(IEnumerable<TValue> other)
        {
            return IsProperSubsetOfAsync(other).GetAwaiter().GetResult();
        }

        public bool IsProperSupersetOf(IEnumerable<TValue> other)
        {
            return IsProperSupersetOfAsync(other).GetAwaiter().GetResult();
        }

        public bool IsSubsetOf(IEnumerable<TValue> other)
        {
            return IsSubsetOfAsync(other).GetAwaiter().GetResult();
        }

        public bool IsSupersetOf(IEnumerable<TValue> other)
        {
            return IsSupersetOfAsync(other).GetAwaiter().GetResult();
        }

        public bool Overlaps(IEnumerable<TValue> other)
        {
            return OverlapsAsync(other).GetAwaiter().GetResult();
        }

        public bool SetEquals(IEnumerable<TValue> other)
        {
            return SetEqualsAsync(other).GetAwaiter().GetResult();
        }

        public void SymmetricExceptWith(IEnumerable<TValue> other)
        {
            SymmetricExceptWithAsync(other).GetAwaiter().GetResult();
        }

        public void UnionWith(IEnumerable<TValue> other)
        {
            UnionWithAsync(other).GetAwaiter().GetResult();
        }

        public bool Contains(TValue item)
        {
            return ContainsAsync(item).GetAwaiter().GetResult();
        }

        public void CopyTo(TValue[] array, int arrayIndex)
        {
           CopyToAsync(array, arrayIndex).GetAwaiter().GetResult();
        }

        public bool Remove(TValue item)
        {
            return RemoveAsync(item).GetAwaiter().GetResult();
        }

        public bool IsReadOnly => false;

        void ICollection<TValue>.Add(TValue item)
        {
            AddAsync(item).GetAwaiter().GetResult();
        }

        public async Task ExceptWithAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            thisSet.ExceptWith(other);
            await Collection.UpsertAsync(Key, thisSet).ConfigureAwait(false);
        }

        public async Task IntersectWithAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            thisSet.IntersectWith(other);
            await Collection.UpsertAsync(Key, thisSet).ConfigureAwait(false);
        }

        public async Task<bool> IsProperSubsetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            return thisSet.IsProperSubsetOf(other);
        }

        public async Task<bool> IsProperSupersetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            return thisSet.IsProperSupersetOf(other);
        }

        public async Task<bool> IsSubsetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            return thisSet.IsSubsetOf(other);
        }

        public async Task<bool> IsSupersetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            return thisSet.IsSupersetOf(other);
        }

        public async Task<bool> OverlapsAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            return thisSet.Overlaps(other);
        }

        public async Task<bool> SetEqualsAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            return thisSet.SetEquals(other);
        }

        public async Task SymmetricExceptWithAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            thisSet.SymmetricExceptWith(other);
            await Collection.UpsertAsync(Key, thisSet).ConfigureAwait(false);
        }

        public async Task UnionWithAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            thisSet.UnionWith(other);
            await Collection.UpsertAsync(Key, thisSet).ConfigureAwait(false);
        }

        public async Task<bool> ContainsAsync(TValue item)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var items = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            return items.Contains(item);
        }

        public async Task<bool> RemoveAsync(TValue item)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key).ConfigureAwait(false);
            var thisSet = getResult.ContentAs<HashSet<TValue>>().EnsureNotNullForDataStructures();
            var removed = thisSet.Remove(item);
            if (removed)
            {
                await Collection.UpsertAsync(Key, thisSet).ConfigureAwait(false);
            }

            return removed;
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
