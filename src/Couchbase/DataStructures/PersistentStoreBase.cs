using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.DataStructures
{
    public abstract class PersistentStoreBase<TValue>: System.Collections.ICollection
    {
        private static readonly ILogger Log = LogManager.CreateLogger<PersistentStoreBase<TValue>>();
        protected  readonly ICollection Collection;
        protected readonly string Key;
        protected bool BackingStoreChecked;

        internal PersistentStoreBase(ICollection collection, string key, object syncRoot, bool isSynchronized)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            SyncRoot = syncRoot;
            IsSynchronized = isSynchronized;
        }

        protected virtual void CreateBackingStore()
        {
            if (BackingStoreChecked) return;
            try
            {
                Collection.InsertAsync(Key, new List<TValue>()).GetAwaiter().GetResult();
                BackingStoreChecked = true;
            }
            catch (KeyExistsException e)
            {
                //ignore - the doc already exists for this collection
                Log.LogTrace(e, $"The PersistentList backing document already exists for ID {Key}. Not an error.");
            }
        }
        protected virtual IList<TValue> GetList()
        {
            return GetListAsync().GetAwaiter().GetResult();
        }

        protected virtual async Task<IList<TValue>> GetListAsync()
        {
            CreateBackingStore();
            using (var result = await Collection.GetAsync(Key).ConfigureAwait(false))
            {
                return result.ContentAs<IList<TValue>>();
            }
        }

        public void Clear()
        {
            ClearAsync().GetAwaiter().GetResult();
        }

        public async Task ClearAsync()
        {
            CreateBackingStore();
            var result = await Collection.
                UpsertAsync(Key, new List<TValue>()).ConfigureAwait(false);
        }

        public Task CopyToAsync(Array array, int index)
        {
            return CopyToAsync((TValue[]) array, index);
        }

        public void CopyTo(Array array, int index)
        {
            CopyToAsync(array, index).GetAwaiter().GetResult();
        }

        public async Task CopyToAsync(TValue[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new IndexOutOfRangeException();

            using (var result = await Collection.GetAsync(Key).ConfigureAwait(false))
            {
                var items = result.ContentAs<IList<TValue>>();
                items.CopyTo(array, arrayIndex);
                await Collection.UpsertAsync(Key, items, options=> options.Cas = result.Cas);
            }
        }

        public Task<int> CountAsync => Task.FromResult(GetListAsync().GetAwaiter().GetResult().Count);

        public int Count => GetList().Count;

        public bool IsSynchronized { get; }

        public object SyncRoot { get; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetListAsync().GetAwaiter().GetResult().GetEnumerator();
        }
    }
}
