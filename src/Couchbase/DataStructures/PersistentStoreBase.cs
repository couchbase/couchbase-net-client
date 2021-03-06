using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.DataStructures
{
    public abstract class PersistentStoreBase<TValue>: System.Collections.ICollection
    {
        protected ILogger? Logger { get; }
        protected ICouchbaseCollection Collection { get; }
        protected string Key { get; }
        protected bool BackingStoreChecked {get; set;}
        internal IRedactor? Redactor;

        internal PersistentStoreBase(ICouchbaseCollection collection, string key, ILogger? logger, IRedactor? redactor, object syncRoot, bool isSynchronized)
        {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Logger = logger;
            Redactor = redactor;
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
            catch (DocumentExistsException e)
            {
                //ignore - the doc already exists for this collection
                Logger?.LogTrace(e, "The PersistentList backing document already exists for ID {key}. Not an error.",
                    Redactor?.UserData(Key));
            }
        }
        protected virtual IList<TValue> GetList()
        {
            return GetListAsync().GetAwaiter().GetResult();
        }

        protected virtual async Task<IList<TValue>> GetListAsync()
        {
            CreateBackingStore();
            using var result = await Collection.GetAsync(Key).ConfigureAwait(false);
            return result.ContentAs<IList<TValue>>();
        }

        public void Clear()
        {
            ClearAsync().GetAwaiter().GetResult();
        }

        public async Task ClearAsync()
        {
            CreateBackingStore();
            await Collection.
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

            using var result = await Collection.GetAsync(Key).ConfigureAwait(false);
            var items = result.ContentAs<IList<TValue>>();
            items.CopyTo(array, arrayIndex);
            await Collection.UpsertAsync(Key, items).ConfigureAwait(false);
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
