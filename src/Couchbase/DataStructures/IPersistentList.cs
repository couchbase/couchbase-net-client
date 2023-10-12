using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.DataStructures
{
    /// <summary>
    /// Represents an <see cref="IList{T}"/> which is persisted to a backing store.
    /// </summary>
    /// <typeparam name="T">Type of value in the set.</typeparam>
    /// <remarks>
    /// If using a <see cref="SystemTextJsonSerializer"/> backed by a <see cref="JsonSerializerContext"/>,
    /// be sure to include <see cref="IList{T}"/> in a <see cref="JsonSerializableAttribute"/> on the context.
    /// Note that a reference comparision is used by default. If you load the
    /// list and try to use an item out of it to remove from the list, it will
    /// return false unless Object.Equals() is overridden on the item's class as
    /// the document is reloaded from the database and therefore cannot be used
    /// for reference equality. Note that .NET Records override Equals implicitly.
    /// </remarks>
    public interface IPersistentList<T> : System.Collections.ICollection, IList<T>, IAsyncEnumerable<T>
    {
        /// <summary>
        /// Copies an items into an array starting at an index.
        /// </summary>
        /// <param name="array">The array of items to add to the document.</param>
        /// <param name="index">The starting index.</param>
        /// <returns></returns>
        Task CopyToAsync(Array array, int index);

        /// <summary>
        /// Adds an item into the document.
        /// </summary>
        /// <remarks>
        /// Override Object.Equals if using POCOs; .NET Records do so implicitly.
        /// </remarks>
        /// <param name="item">The item to add.</param>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task AddAsync(T item);

        /// <summary>
        /// Clears the document.
        /// </summary>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task ClearAsync();

        /// <summary>
        /// Checks to see if the document contains an item.
        /// </summary>
        /// <remarks>
        /// Override Object.Equals if using POCOs; .NET Records do so implicitly.
        /// </remarks>
        /// <param name="item">The item <typeparamref name="T"/> to check for its existence.</param>
        /// <returns>A <see cref="Task{Bool}"/> for awaiting. True if the item exists, otherwise false.</returns>
        Task<bool> ContainsAsync(T item);

        /// <summary>
        /// Copies an array into the document.
        /// </summary>
        /// <param name="array">The array of items to add to the document.</param>
        /// <param name="arrayIndex">The starting index.</param>
        /// <returns>A <see cref="Task"/> for awaiting.</returns>
        Task CopyToAsync(T[] array, int arrayIndex);

        /// <summary>
        /// Attempts to remove an item from the list.
        /// </summary>
        /// <remarks>
        /// Override Object.Equals if using POCOs; .NET Records do so implicitly.
        /// </remarks>
        /// <param name="item">An item which should have Object.Equals() overridden.</param>
        /// <returns>True if the item is found and removed; otherwise false.</returns>
        Task<bool> RemoveAsync(T item);

        /// <summary>
        /// Counts the items in the list.
        /// </summary>
        /// <returns>A <see cref="Task{Int32}"/> for awaiting. The value is the number of items in the list.</returns>
        Task<int> CountAsync { get; }

        /// <summary>
        /// Returns the index of item in the list.
        /// </summary>
        /// <remarks>
        /// Override Object.Equals if using POCOs; .NET Records do so implicitly.
        /// </remarks>
        /// <param name="item">An item which should have Object.Equals() overridden.</param>
        /// <returns>A <see cref="Task{Int32}"/> for awaiting. The value is the index of item in the list.</returns>
        Task<int> IndexOfAsync(T item);

        /// <summary>
        /// Inserts an item into the list.
        /// </summary>
        /// <remarks>
        /// Override Object.Equals if using POCOs; .NET Records do so implicitly.
        /// </remarks>
        /// <param name="index">The starting index.</param>
        /// <param name="item">An item which should have Object.Equals() overridden.</param>
        /// <returns></returns>
        Task InsertAsync(int index, T item);

        /// <summary>
        /// Removes an item item from the list.
        /// </summary>
        /// <remarks>
        /// Override Object.Equals if using POCOs; .NET Records do so implicitly.
        /// </remarks>
        /// <param name="index">The starting index.</param>
        /// <returns>A <see cref="Task"/> for awaiting."</returns>
        Task RemoveAtAsync(int index);
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
