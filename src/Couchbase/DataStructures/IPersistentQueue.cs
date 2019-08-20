using System.Threading.Tasks;

namespace Couchbase.DataStructures
{
    public interface IPersistentQueue<T> :  System.Collections.ICollection
    {
        T Dequeue();

        Task<T> DequeueAsync();

        void Enqueue(T item);

        Task EnqueueAsync(T item);

        T Peek();

        Task<T> PeekAsync();

        void Clear();

        Task ClearAsync();
    }
}
