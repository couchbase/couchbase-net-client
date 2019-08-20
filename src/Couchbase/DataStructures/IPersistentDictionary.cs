using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.DataStructures
{
    public interface IPersistentDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        Task AddAsync(KeyValuePair<TKey, TValue> item);

        Task ClearAsync();

        Task<bool> ContainsAsync(KeyValuePair<TKey, TValue> item);

        Task<bool> RemoveAsync(KeyValuePair<TKey, TValue> item);

        Task<int> CountAsync { get; }

        Task AddAsync(TKey key, TValue value);

        Task<bool> ContainsKeyAsync(TKey key);

        Task<bool> RemoveAsync(TKey key);

        Task<ICollection<TKey>> KeysAsync { get; }

        Task<ICollection<TValue>> ValuesAsync { get; }
    }
}
