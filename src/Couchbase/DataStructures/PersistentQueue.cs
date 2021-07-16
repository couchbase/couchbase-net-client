using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.DataStructures
{
    public class PersistentQueue<TValue> : PersistentStoreBase<TValue>, IPersistentQueue<TValue>
    {
        internal PersistentQueue(ICouchbaseCollection collection, string key, ILogger? logger, IRedactor? redactor)
            : base(collection, key, logger, redactor, new object(), false)
        {
        }

        public TValue Dequeue()
        {
            return DequeueAsync().GetAwaiter().GetResult();
        }

        public async Task<TValue> DequeueAsync()
        {
            CreateBackingStore();
            var result = await Collection.LookupInAsync(Key, builder => builder.Get("[0]")).ConfigureAwait(false);
            var item = result.ContentAs<TValue>(0);

            await Collection.MutateInAsync(Key, builder => builder.Remove("[0]"),
                options => options.Cas(result.Cas)).ConfigureAwait(false);

            return item;
        }

        public void Enqueue(TValue item)
        {
            EnqueueAsync(item).GetAwaiter().GetResult();
        }

        public async Task EnqueueAsync(TValue item)
        {
            CreateBackingStore();
            await Collection.MutateInAsync(Key, builder => builder.ArrayAppend("", item)).ConfigureAwait(false);
        }

        public TValue Peek()
        {
            return PeekAsync().GetAwaiter().GetResult();
        }

        public async Task<TValue> PeekAsync()
        {
            var result = await Collection.LookupInAsync(Key, builder => builder.Get("[0]")).ConfigureAwait(false);
            return result.ContentAs<TValue>(0);
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
