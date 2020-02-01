using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using ICollection = Couchbase.KeyValue.ICollection;

#nullable enable

namespace Couchbase.DataStructures
{
    public class PersistentDictionary<TKey, TValue> : IPersistentDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly ILogger? _logger;
        protected ICollection Collection { get; }
        protected string DocId { get; }
        protected bool BackingStoreChecked { get; set; }

        internal PersistentDictionary(ICollection collection, string docId, ILogger? logger)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            DocId = docId ?? throw new ArgumentNullException(nameof(docId));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected virtual void CreateBackingStore()
        {
            if (BackingStoreChecked) return;
            try
            {
                Collection.InsertAsync(DocId, new Dictionary<TKey, TValue>()).GetAwaiter().GetResult();
                BackingStoreChecked = true;
            }
            catch (DocumentExistsException e)
            {
                //ignore - the doc already exists for this collection
                _logger?.LogTrace(e, "The PersistentDictionary backing document already exists for ID {key}. Not an error.", DocId);
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            CreateBackingStore();
            using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
            return result.ContentAs<IEnumerator<KeyValuePair<TKey, TValue>>>();
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
            using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
            var dict = result.ContentAs<IDictionary<TKey, TValue>>();
            dict.CopyTo(array, arrayIndex);
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

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            CreateBackingStore();
            var success = true;
            try
            {
                using var result = Collection.LookupInAsync(DocId, builder => builder.Get(key.ToString()))
                    .GetAwaiter().GetResult();
                value = result.ContentAs<TValue>(0);
            }
            catch (Exception e)
            {
                value = default!;
                success = false;
                _logger?.LogDebug(e, "Error fetching value for key {key}.", key);
            }

            return success;
        }

        public TValue this[TKey key]
        {
            get
            {
                if (TryGetValue(key, out TValue value))
                    return value;
                throw new KeyNotFoundException($"Cannot find key {key}.");
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
                using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
                return Task.FromResult(result.ContentAs<IDictionary<TKey, TValue>>().Count);
            }
        }

        public async Task AddAsync(TKey key, TValue value)
        {
            CreateBackingStore();
            using var exists = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString()));
            if (exists.Exists(0))
            {
                throw new ArgumentException("An element with the same key already exists in the Dictionary.");
            }

            await Collection.MutateInAsync(DocId, builder => builder.Insert(DocId, value),
                options => options.Cas(exists.Cas));
        }

        public async Task<bool> ContainsKeyAsync(TKey key)
        {
            CreateBackingStore();
            using var result = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString()));
            return result.Exists(0);
        }

        public async Task<bool> RemoveAsync(TKey key)
        {
            CreateBackingStore();
            var success = true;
            try
            {
                await Collection.MutateInAsync(DocId, builder => builder.Remove(key.ToString()));
            }
            catch (Exception e)
            {
                success = false;
                _logger?.LogDebug(e, "Remove failed.");
            }

            return success;
        }

        public Task<ICollection<TKey>> KeysAsync
        {
            get
            {
                CreateBackingStore();
                using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
                return Task.FromResult(result.ContentAs<IDictionary<TKey, TValue>>().Keys);
            }
        }

        public Task<ICollection<TValue>> ValuesAsync
        {
            get
            {
                CreateBackingStore();
                using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
                return Task.FromResult(result.ContentAs<IDictionary<TKey, TValue>>().Values);
            }
        }
    }
}
