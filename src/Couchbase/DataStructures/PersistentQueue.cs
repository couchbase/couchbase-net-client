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
