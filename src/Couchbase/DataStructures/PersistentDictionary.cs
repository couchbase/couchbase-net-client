using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.DataStructures
{
    public class PersistentDictionary<TValue> : IPersistentDictionary<TValue>
    {
        private readonly ILogger? _logger;
        private readonly IRedactor? _redactor;
        protected ICouchbaseCollection Collection { get; }
        protected string DocId { get; }
        protected bool BackingStoreChecked { get; set; }

        internal PersistentDictionary(ICouchbaseCollection collection, string docId, ILogger? logger, IRedactor? redactor)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            DocId = docId ?? throw new ArgumentNullException(nameof(docId));
            _logger = logger;
            _redactor = redactor;
        }

        protected virtual void CreateBackingStore()
        {
            if (BackingStoreChecked) return;
            try
            {
                Collection.InsertAsync(DocId, new Dictionary<string, TValue>()).GetAwaiter().GetResult();
                BackingStoreChecked = true;
            }
            catch (DocumentExistsException e)
            {
                //ignore - the doc already exists for this collection
                _logger?.LogTrace(e,
                    "The PersistentDictionary backing document already exists for ID {key}. Not an error.",
                    _redactor?.UserData(DocId));
            }
        }

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
        {
            CreateBackingStore();
            using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
            return result.ContentAs<IEnumerator<KeyValuePair<string, TValue>>>();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<string, TValue> item)
        {
            AddAsync(item).GetAwaiter().GetResult();
        }

        public void Clear()
        {
            ClearAsync().GetAwaiter().GetResult();
        }

        public bool Contains(KeyValuePair<string, TValue> item)
        {
            return ContainsAsync(item).GetAwaiter().GetResult();
        }

        public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
        {
            CreateBackingStore();
            using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
            var dict = result.ContentAs<IDictionary<string, TValue>>();
            dict.CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, TValue> item)
        {
           return RemoveAsync(item).GetAwaiter().GetResult();
        }

        public int Count => CountAsync.GetAwaiter().GetResult();

        public bool IsReadOnly => false;

        public void Add(string key, TValue value)
        {
            AddAsync(key, value).GetAwaiter().GetResult();
        }

        public bool ContainsKey(string key)
        {
            return ContainsKeyAsync(key).GetAwaiter().GetResult();
        }

        public bool Remove(string key)
        {
            return RemoveAsync(key).GetAwaiter().GetResult();
        }

#pragma warning disable CS8767
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
#pragma warning restore CS8767
        {
            CreateBackingStore();
            var success = true;
            try
            {
                var result = Collection.LookupInAsync(DocId, builder => builder.Get(key.ToString()))
                    .GetAwaiter().GetResult();
                value = result.ContentAs<TValue>(0);
            }
            catch (Exception e)
            {
                value = default!;
                success = false;
                _logger?.LogDebug(e, "Error fetching value for key {key}.", _redactor?.UserData(key));
            }

            return success;
        }

        public TValue this[string key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                    return value;
                throw new KeyNotFoundException($"Cannot find key {key.ToString()}.");
            }
            set => Add(key, value);
        }

        public ICollection<string> Keys => KeysAsync.GetAwaiter().GetResult();

        public ICollection<TValue> Values => ValuesAsync.GetAwaiter().GetResult();

        public async Task AddAsync(KeyValuePair<string, TValue> item)
        {
            CreateBackingStore();
            await Collection.MutateInAsync(DocId, builder => builder.Insert(item.Key.ToString(), item.Value)).ConfigureAwait(false);
        }

        public Task ClearAsync()
        {
           return Collection.UpsertAsync(DocId, new Dictionary<string, TValue>());
        }

        public Task<bool> ContainsAsync(KeyValuePair<string, TValue> item)
        {
            return ContainsKeyAsync(item.Key);
        }

        public Task<bool> RemoveAsync(KeyValuePair<string, TValue> item)
        {
            return RemoveAsync(item.Key);
        }

        public Task<int> CountAsync
        {
            get
            {
                CreateBackingStore();
                using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
                return Task.FromResult(result.ContentAs<IDictionary<string, TValue>>().Count);
            }
        }

        public async Task AddAsync(string key, TValue value)
        {
            CreateBackingStore();
            var exists = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString())).ConfigureAwait(false);
            if (exists.Exists(0))
            {
                throw new ArgumentException("An element with the same key already exists in the Dictionary.");
            }

            await Collection.MutateInAsync(DocId, builder => builder.Insert(DocId, value),
                options => options.Cas(exists.Cas)).ConfigureAwait(false);
        }

        public async Task<bool> ContainsKeyAsync(string key)
        {
            CreateBackingStore();
            var result = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString())).ConfigureAwait(false);
            return result.Exists(0);
        }

        public async Task<bool> RemoveAsync(string key)
        {
            CreateBackingStore();
            var success = true;
            try
            {
                await Collection.MutateInAsync(DocId, builder => builder.Remove(key.ToString())).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                success = false;
                _logger?.LogDebug(e, "Remove failed.");
            }

            return success;
        }

        public Task<ICollection<string>> KeysAsync
        {
            get
            {
                CreateBackingStore();
                using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
                return Task.FromResult(result.ContentAs<IDictionary<string, TValue>>().Keys);
            }
        }

        public Task<ICollection<TValue>> ValuesAsync
        {
            get
            {
                CreateBackingStore();
                using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
                return Task.FromResult(result.ContentAs<IDictionary<string, TValue>>().Values);
            }
        }
    }
}
