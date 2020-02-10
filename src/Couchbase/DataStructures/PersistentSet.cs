using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using ICollection = Couchbase.KeyValue.ICollection;

#nullable enable

namespace Couchbase.DataStructures
{
    public class PersistentSet<TValue> : PersistentStoreBase<TValue>, IPersistentSet<TValue>
    {
        internal PersistentSet(ICollection collection, string key, ILogger? logger, IRedactor? redactor)
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
            return result.ContentAs<ISet<TValue>>();
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
            using var getResult = await Collection.GetAsync(Key);
            var items = getResult.ContentAs<HashSet<TValue>>();
            var added = items.Add(item);
            if (added)
            {
                await Collection.UpsertAsync(Key, items);
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
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            thisSet.ExceptWith(other);
            await Collection.UpsertAsync(Key, thisSet);
        }

        public async Task IntersectWithAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            thisSet.IntersectWith(other);
            await Collection.UpsertAsync(Key, thisSet);
        }

        public async Task<bool> IsProperSubsetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            return thisSet.IsProperSubsetOf(other);
        }

        public async Task<bool> IsProperSupersetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            return thisSet.IsProperSupersetOf(other);
        }

        public async Task<bool> IsSubsetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            return thisSet.IsSubsetOf(other);
        }

        public async Task<bool> IsSupersetOfAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            return thisSet.IsSupersetOf(other);
        }

        public async Task<bool> OverlapsAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            return thisSet.Overlaps(other);
        }

        public async Task<bool> SetEqualsAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            return thisSet.SetEquals(other);
        }

        public async Task SymmetricExceptWithAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            thisSet.SymmetricExceptWith(other);
            await Collection.UpsertAsync(Key, thisSet);
        }

        public async Task UnionWithAsync(IEnumerable<TValue> other)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            thisSet.UnionWith(other);
            await Collection.UpsertAsync(Key, thisSet);
        }

        public async Task<bool> ContainsAsync(TValue item)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var items = getResult.ContentAs<HashSet<TValue>>();
            return items.Contains(item);
        }

        public async Task<bool> RemoveAsync(TValue item)
        {
            CreateBackingStore();
            using var getResult = await Collection.GetAsync(Key);
            var thisSet = getResult.ContentAs<HashSet<TValue>>();
            var removed = thisSet.Remove(item);
            if (removed)
            {
                await Collection.UpsertAsync(Key, thisSet);
            }

            return removed;
        }
    }
}
