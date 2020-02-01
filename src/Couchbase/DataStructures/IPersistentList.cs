using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.DataStructures
{
    public interface IPersistentList<T> : System.Collections.ICollection, IList<T>
    {
        Task CopyToAsync(Array array, int index);

        Task AddAsync(T item);

        Task ClearAsync();

        Task<bool> ContainsAsync(T item);

        Task CopyToAsync(T[] array, int arrayIndex);

        Task<bool> RemoveAsync(T item);

        Task<int> CountAsync { get; }

        Task<int> IndexOfAsync(T item);

        Task InsertAsync(int index, T item);

        Task RemoveAtAsync(int index);
    }
}
