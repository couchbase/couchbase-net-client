using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Services.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.DataStructures
{
    public class PersistentQueue<TValue> : PersistentStoreBase<TValue>, IPersistentQueue<TValue>
    {
        private static readonly ILogger Log = LogManager.CreateLogger<PersistentList<TValue>>();

        internal PersistentQueue(ICollection collection, string key)
            : base(collection, key, new object(), false)
        {
        }

        public TValue Dequeue()
        {
            return DequeueAsync().GetAwaiter().GetResult();
        }

        public async Task<TValue> DequeueAsync()
        {
            CreateBackingStore();
            var result = await Collection.LookupInAsync(Key, builder => builder.Get("[0]"));
            var item = result.ContentAs<TValue>(0);

            var mutateResult = await Collection.MutateInAsync(Key, builder => builder.Remove("[0]"),
                options => options.WithCas(result.Cas));

            return item;
        }

        public void Enqueue(TValue item)
        {
            EnqueueAsync(item).GetAwaiter().GetResult();
        }

        public async Task EnqueueAsync(TValue item)
        {
            CreateBackingStore();
            var result = await Collection.MutateInAsync(Key, builder => builder.ArrayAppend("", item));
        }

        public TValue Peek()
        {
            return PeekAsync().GetAwaiter().GetResult();
        }

        public async Task<TValue> PeekAsync()
        {
            var result = await Collection.LookupInAsync(Key, builder => builder.Get("[0]"));
            return result.ContentAs<TValue>(0);
        }
    }
}
