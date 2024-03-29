using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.DataStructures
{
    /// <summary>
    /// Represents an <see cref="ISet{T}"/> which is persisted to a backing store.
    /// </summary>
    /// <typeparam name="TValue">Type of value in the set.</typeparam>
    /// <remarks>
    /// If using a <see cref="SystemTextJsonSerializer"/> backed by a <see cref="JsonSerializerContext"/>,
    /// be sure to include <see cref="ISet{T}"/> in a <see cref="JsonSerializableAttribute"/> on the context.
    /// </remarks>
    public interface IPersistentSet<TValue> : ISet<TValue>
    {
        Task<bool> AddAsync(TValue item);

        Task ExceptWithAsync(IEnumerable<TValue> other);

        Task IntersectWithAsync(IEnumerable<TValue> other);

        Task<bool> IsProperSubsetOfAsync(IEnumerable<TValue> other);

        Task<bool> IsProperSupersetOfAsync(IEnumerable<TValue> other);

        Task<bool> IsSubsetOfAsync(IEnumerable<TValue> other);

        Task<bool> IsSupersetOfAsync(IEnumerable<TValue> other);

        Task<bool> OverlapsAsync(IEnumerable<TValue> other);

        Task<bool> SetEqualsAsync(IEnumerable<TValue> other);

        Task SymmetricExceptWithAsync(IEnumerable<TValue> other);

        Task UnionWithAsync(IEnumerable<TValue> other);

        Task ClearAsync();

        Task<bool> ContainsAsync(TValue item);

        Task CopyToAsync(TValue[] array, int arrayIndex);

        Task<bool> RemoveAsync(TValue item);

        Task<int> CountAsync { get; }
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
