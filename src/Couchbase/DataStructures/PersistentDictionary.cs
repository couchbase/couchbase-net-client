using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using System.Threading;

#nullable enable

namespace Couchbase.DataStructures
{
    public sealed class PersistentDictionary<TValue> : IPersistentDictionary<TValue>
    {
        private readonly ILogger? _logger;
        private readonly IRedactor? _redactor;
        private ICouchbaseCollection Collection { get; }
        private string DocId { get; }
        private bool BackingStoreChecked { get; set; }

        internal PersistentDictionary(ICouchbaseCollection collection, string docId, ILogger? logger, IRedactor? redactor)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            DocId = docId ?? throw new ArgumentNullException(nameof(docId));
            _logger = logger;
            _redactor = redactor;
        }

        private async ValueTask CreateBackingStoreAsync()
        {
            if (BackingStoreChecked) return;
            try
            {
                await Collection.InsertAsync(DocId, new Dictionary<string, TValue>()).ConfigureAwait(false);
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
            CreateBackingStoreAsync().GetAwaiter().GetResult();
            using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
            return result.ContentAs<ReadOnlyDictionary<string, TValue>>()
                .EnsureNotNullForDataStructures().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public async IAsyncEnumerator<KeyValuePair<string, TValue>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            using var result = await Collection.GetAsync(DocId).ConfigureAwait(false);
            var content = result.ContentAs<ReadOnlyDictionary<string, TValue>>()
                .EnsureNotNullForDataStructures();

            foreach (var item in content) yield return item;
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
            CreateBackingStoreAsync().GetAwaiter().GetResult();
            using var result = Collection.GetAsync(DocId).GetAwaiter().GetResult();
            var dict = result.ContentAs<IDictionary<string, TValue>>().EnsureNotNullForDataStructures();
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
            CreateBackingStoreAsync().ConfigureAwait(false);
            var success = true;
            try
            {
                var result = Collection.LookupInAsync(DocId, builder => builder.Get(key.ToString()))
                    .GetAwaiter().GetResult();
                value = result.ContentAs<TValue>(0);
            }
            catch (PathNotFoundException)
            {
                value = default!;
                return false;
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
                throw new KeyNotFoundException($"Cannot find key {key}.");
            }
            set => SetAsync(key, value).GetAwaiter().GetResult();
        }

        public ICollection<string> Keys => KeysAsync.GetAwaiter().GetResult();

        public ICollection<TValue> Values => ValuesAsync.GetAwaiter().GetResult();

        public async Task AddAsync(KeyValuePair<string, TValue> item)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            await Collection.MutateInAsync(DocId, builder => builder.Insert(item.Key.ToString(), item.Value)).ConfigureAwait(false);
        }

        public async Task ClearAsync()
        {
            await Collection.UpsertAsync(DocId, new Dictionary<string, TValue>()).ConfigureAwait(false);
            BackingStoreChecked = true;
        }

        public Task<bool> ContainsAsync(KeyValuePair<string, TValue> item)
        {
            return ContainsKeyAsync(item.Key);
        }

        public Task<bool> RemoveAsync(KeyValuePair<string, TValue> item)
        {
            return RemoveAsync(item.Key);
        }

        public Task<int> CountAsync => Task.Run(async () =>
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            using var result = await Collection.GetAsync(DocId).ConfigureAwait(false);
            return result.ContentAs<IDictionary<string, TValue>>().EnsureNotNullForDataStructures().Count;
        });

        public async Task AddAsync(string key, TValue value)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            var exists = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString())).ConfigureAwait(false);
            if (exists.Exists(0))
            {
                throw new ArgumentException("An element with the same key already exists in the Dictionary.");
            }

            await Collection.MutateInAsync(DocId, builder => builder.Insert(key, value),
                options => options.Cas(exists.Cas)).ConfigureAwait(false);
        }

        public async Task<bool> ContainsKeyAsync(string key)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            var result = await Collection.LookupInAsync(DocId, builder => builder.Exists(key.ToString())).ConfigureAwait(false);
            return result.Exists(0);
        }
        public async Task<TValue> GetAsync(string key)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);

            try
            {
                var result = await Collection.LookupInAsync(DocId, builder => builder.Get(key)).ConfigureAwait(false);
                return result.ContentAs<TValue>(0)!;
            }
            catch (PathNotFoundException)
            {
                throw new KeyNotFoundException($"Cannot find key {key}.");
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
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

        public async Task SetAsync(string key, TValue value)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);

            await Collection.MutateInAsync(DocId, builder => builder.Upsert(key, value)).ConfigureAwait(false);
        }

        public Task<ICollection<string>> KeysAsync => KeysInternalAsync();
        private async Task<ICollection<string>> KeysInternalAsync()
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            using var result = await Collection.GetAsync(DocId).ConfigureAwait(false);
            return result.ContentAs<IDictionary<string, TValue>>().EnsureNotNullForDataStructures().Keys;
        }

        public Task<ICollection<TValue>> ValuesAsync => ValuesInternalAsync();

        private async Task<ICollection<TValue>> ValuesInternalAsync()
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            using var result = await Collection.GetAsync(DocId).ConfigureAwait(false);
            return result.ContentAs<IDictionary<string, TValue>>().EnsureNotNullForDataStructures().Values;
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
