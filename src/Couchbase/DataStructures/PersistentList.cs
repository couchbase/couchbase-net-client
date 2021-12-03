using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.DataStructures
{
    public sealed class PersistentList<TValue> : PersistentStoreBase<TValue>, IPersistentList<TValue>
    {
        internal PersistentList(ICouchbaseCollection collection, string key, ILogger? logger, IRedactor? redactor)
            : base(collection, key, logger, redactor, new object(), false)
        {
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return GetList().GetEnumerator();
        }

        public void Add(TValue item)
        {
            AddAsync(item).GetAwaiter().GetResult();
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

        int ICollection<TValue>.Count => base.Count;

        public bool IsReadOnly => false;

        public int IndexOf(TValue item)
        {
            return IndexOfAsync(item).GetAwaiter().GetResult();
        }

        public void Insert(int index, TValue item)
        {
            InsertAsync(index, item).GetAwaiter().GetResult();
        }

        public void RemoveAt(int index)
        {
            RemoveAtAsync(index).GetAwaiter().GetResult();
        }

        public TValue this[int index]
        {
            get => GetList()[index];
            set => SetAsync(index, value).GetAwaiter().GetResult();
        }

        public async Task AddAsync(TValue item)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            await Collection.
                MutateInAsync(Key, builder => builder.ArrayAppend("", item)).
                    ConfigureAwait(false);
        }

        public async Task<bool> ContainsAsync(TValue item)
        {
            return (await GetListAsync().ConfigureAwait(false)).Contains(item);
        }

        public async Task<bool> RemoveAsync(TValue item)
        {
            var index = await IndexOfAsync(item).ConfigureAwait(false);
            var removed = false;
            try
            {
                if (index >= 0)
                {
                    await RemoveAtAsync(index).ConfigureAwait(false);
                    removed = true;
                }
            }
            catch (Exception e)
            {
                Logger?.LogError(e, "Item could not be removed from PersistentList with ID of {key}",
                    Redactor?.UserData(Key));
            }

            return removed;
        }

        public async Task<int> IndexOfAsync(TValue item)
        {
            return (await GetListAsync().ConfigureAwait(false)).IndexOf(item);
        }

        public async Task InsertAsync(int index, TValue item)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            await Collection.
                MutateInAsync(Key, builder => builder.ArrayInsert($"[{index}]", new[] {item})).
                ConfigureAwait(false);
        }

        public async Task SetAsync(int index, TValue item)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);

            try
            {
                await Collection.MutateInAsync(Key, builder => builder.Replace($"[{index}]", item))
                    .ConfigureAwait(false);
            }
            catch (PathNotFoundException)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public async Task RemoveAtAsync(int index)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            await Collection.
                MutateInAsync(Key, builder => builder.Remove($"[{index}]")).
                ConfigureAwait(false);
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
