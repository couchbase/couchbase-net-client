using System.Collections.Concurrent;

namespace Couchbase.Utils
{
    internal class LimitedConcurrentQueue<T> : ConcurrentQueue<T>
    {
        public int MaxCapacity { get; }

        public LimitedConcurrentQueue(int maxCapacity)
        {
            MaxCapacity = maxCapacity;
        }

        public new void Enqueue(T item)
        {
            if (Count < MaxCapacity)
            {
                base.Enqueue(item);
            }
        }
    }
}
