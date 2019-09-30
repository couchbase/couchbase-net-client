using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using ICollection = Couchbase.KeyValue.ICollection;

namespace Couchbase.DataStructures
{
    public class PersistentDictionary<TKey, TValue> : IPersistentDictionary<TKey, TValue>
    {
        private static readonly ILogger Log = LogManager.CreateLogger<PersistentDictionary<TKey, TValue>>();
        protected  readonly ICollection Collection;
        protected readonly string DocId;
        protected bool BackingStoreChecked;

        internal PersistentDictionary(ICollection collection, string docId)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            DocId = docId ?? throw new ArgumentNullException(nameof(docId));
        }

        protected virtual void CreateBackingStore()
        {
            if (BackingStoreChecked) return;
            try
            {
                Collection.InsertAsync(DocId, new Dictionary<TKey, TValue>()).GetAwaiter().GetResult();
                BackingStoreChecked = true;
            }
            catch (KeyExistsException e)
            {
                //ignore - the doc already exists for this collection
                Log.LogTrace(e, $"The PersistentList backing document already exists for ID {DocId}. Not an error.");
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            CreateBackingStore();
            using (var result = Collection.GetAsync(DocId).GetAwaiter().GetResult())
            {
                return result.ContentAs<IEnumerator<KeyValuePair<TKey, TValue>>>();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            AddAsync(item).GetAwaiter().GetResult();
        }

        public void Clear()
        {
            ClearAsync().GetAwaiter().GetResult();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ContainsAsync(item).GetAwaiter().GetResult();
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            CreateBackingStore();
            using (var result = Collection.GetAsync(DocId).GetAwaiter().GetResult())
            {
                var dict = result.ContentAs<IDictionary<TKey, TValue>>();
                dict.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
           return RemoveAsync(item).GetAwaiter().GetResult();
        }

        public int Count => CountAsync.GetAwaiter().GetResult();

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            AddAsync(key, value).GetAwaiter().GetResult();
        }

        public bool ContainsKey(TKey key)
        {
            return ContainsKeyAsync(key).GetAwaiter().GetResult();
        }

        public bool Remove(TKey key)
        {
            return RemoveAsync(key).GetAwaiter().GetResult();
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            CreateBackingStore();
            var success = true;
            try
            {
                using (var result = Collection.LookupInAsync(DocId, builder => builder.Get(key.ToString()))
                    .GetAwaiter().GetResult())
                {
                    value = result.ContentAs<TValue>(0);
                }
            }
            catch (Exception e)
            {
                value = default;
                success = false;
                Log.LogDebug(e, $"Error fetching value for key {key}.");
            }

            return success;
        }

        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out TValue value))
                    return value;
                throw new System.Collections.Generic.KeyNotFoundException($"Cannot find key {key}.");
            }
            set => Add(key, value);
        }

        public ICollection<TKey> Keys => KeysAsync.GetAwaiter().GetResult();

        public ICollection<TValue> Values => ValuesAsync.GetAwaiter().GetResult();

        public async Task AddAsync(KeyValuePair<TKey, TValue> item)
        {
            CreateBackingStore();
            await Collection.MutateInAsync(DocId, builder => builder.Insert(item.Key.ToString(), item.Value));
        }

        public Task ClearAsync()
        {
           return Collection.UpsertAsync(DocId, new Dictionary<TKey, TValue>());
        }

        public Task<bool> ContainsAsync(KeyValuePair<TKey, TValue> item)
        {
            return ContainsKeyAsync(item.Key);
        }

        public Task<bool> RemoveAsync(KeyValuePair<TKey, TValue> item)
        {
            return RemoveAsync(item.Key);
        }

        public Task<int> CountAsync
        {
            get
            {
                CreateBackingStore();
                using (var result = Collection.GetAsync(DocId).GetAwaiter().GetResult())
                {
                    return Task.FromResult(result.ContentAs<IDictionary<TKey, TValue>>().Count);
                }
            }
        }

        public async Task AddAsync(TKey key, TValue value)
        {
            CreateBackingStore();
            using (var exists = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString())))
            {
                if (exists.Exists(0))
                {
                    throw new ArgumentException("An element with the same key already exists in the Dictionary.");
                }

                await Collection.MutateInAsync(DocId, builder => builder.Insert(DocId, value),
                    options => options.Cas = exists.Cas);
            }
        }

        public async Task<bool> ContainsKeyAsync(TKey key)
        {
            CreateBackingStore();
            using (var result = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString())))
            {
                return result.Exists(0);
            }
        }

        public async Task<bool> RemoveAsync(TKey key)
        {
            CreateBackingStore();
            var success = true;
            try
            {
                var result = await Collection.MutateInAsync(DocId, builder => builder.Remove(key.ToString()));
            }
            catch (Exception e)
            {
                success = false;
                Log.LogDebug(e, "Remove failed.");
            }

            return success;
        }

        public Task<ICollection<TKey>> KeysAsync
        {
            get
            {
                CreateBackingStore();
                using (var result = Collection.GetAsync(DocId).GetAwaiter().GetResult())
                {
                    return Task.FromResult(result.ContentAs<IDictionary<TKey, TValue>>().Keys);
                }
            }
        }

        public Task<ICollection<TValue>> ValuesAsync
        {
            get
            {
                CreateBackingStore();
                using (var result = Collection.GetAsync(DocId).GetAwaiter().GetResult())
                {
                    return Task.FromResult(result.ContentAs<IDictionary<TKey, TValue>>().Values);
                }
            }
        }
    }
}
