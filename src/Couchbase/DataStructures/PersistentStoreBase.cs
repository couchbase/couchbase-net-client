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
    public abstract class PersistentStoreBase<TValue>: System.Collections.ICollection
    {
        protected ILogger? Logger { get; }
        protected ICouchbaseCollection Collection { get; }
        protected string Key { get; }
        protected bool BackingStoreChecked {get; set;}
        internal IRedactor? Redactor;

        internal PersistentStoreBase(ICouchbaseCollection collection, string key, ILogger? logger, IRedactor? redactor, object syncRoot, bool isSynchronized)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (collection == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(collection));
            }
            if (key == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(key));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            Collection = collection;
            Key = key;
            Logger = logger;
            Redactor = redactor;
            SyncRoot = syncRoot;
            IsSynchronized = isSynchronized;
        }

        [Obsolete("Use asynchronous overload.")]
        protected virtual void CreateBackingStore()
        {
            CreateBackingStoreAsync().GetAwaiter().GetResult();
        }

        protected virtual async ValueTask CreateBackingStoreAsync()
        {
            if (BackingStoreChecked) return;
            try
            {
                await Collection.InsertAsync(Key, new List<TValue>()).ConfigureAwait(false);
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
            await CreateBackingStoreAsync().ConfigureAwait(false);
            using var result = await Collection.GetAsync(Key).ConfigureAwait(false);
            return result.ContentAs<IList<TValue>>().EnsureNotNullForDataStructures();
        }

        public void Clear()
        {
            ClearAsync().GetAwaiter().GetResult();
        }

        public async Task ClearAsync()
        {
            await Collection.
                UpsertAsync(Key, new List<TValue>()).ConfigureAwait(false);
            BackingStoreChecked = true;
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
            var items = result.ContentAs<IList<TValue>>().EnsureNotNullForDataStructures();
            items.CopyTo(array, arrayIndex);
        }

        // Use of GetAwaiter().GetResult() here is non-blocking because we know the task is complete.
        // Using that pattern just cleans up behaviors when there is an exception.
        public Task<int> CountAsync => GetListAsync().ContinueWith(task => task.GetAwaiter().GetResult().Count);

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
