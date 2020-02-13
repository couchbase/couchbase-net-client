using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.DataStructures
{
    public interface IPersistentDictionary<TValue> : IDictionary<string, TValue>
    {
        Task AddAsync(KeyValuePair<string, TValue> item);

        Task ClearAsync();

        Task<bool> ContainsAsync(KeyValuePair<string, TValue> item);

        Task<bool> RemoveAsync(KeyValuePair<string, TValue> item);

        Task<int> CountAsync { get; }

        Task AddAsync(string key, TValue value);

        Task<bool> ContainsKeyAsync(string key);

        Task<bool> RemoveAsync(string key);

        Task<ICollection<string>> KeysAsync { get; }

        Task<ICollection<TValue>> ValuesAsync { get; }
    }
}
