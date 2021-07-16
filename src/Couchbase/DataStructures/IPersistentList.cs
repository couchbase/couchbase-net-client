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
