using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.DataStructures
{
    /// <summary>
    /// Represents an <see cref="IDictionary{TKey,TValue}"/> which is persisted to a backing store.
    /// </summary>
    /// <typeparam name="TValue">Type of value in the set.</typeparam>
    /// <remarks>
    /// If using a <see cref="SystemTextJsonSerializer"/> backed by a <see cref="JsonSerializerContext"/>,
    /// be sure to include <c>IDictionary&lt;string, TValue&gt;</c> in a <see cref="JsonSerializableAttribute"/> on the context.
    /// </remarks>
    public interface IPersistentDictionary<TValue> : IDictionary<string, TValue>, IAsyncEnumerable<KeyValuePair<string, TValue>>
    {
        Task AddAsync(KeyValuePair<string, TValue> item);

        Task ClearAsync();

        Task<bool> ContainsAsync(KeyValuePair<string, TValue> item);

        Task<bool> RemoveAsync(KeyValuePair<string, TValue> item);

        Task<int> CountAsync { get; }

        Task AddAsync(string key, TValue value);

        Task<bool> ContainsKeyAsync(string key);

        Task<TValue> GetAsync(string key);

        Task<bool> RemoveAsync(string key);

        Task SetAsync(string key, TValue value);

        Task<ICollection<string>> KeysAsync { get; }

        Task<ICollection<TValue>> ValuesAsync { get; }
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
