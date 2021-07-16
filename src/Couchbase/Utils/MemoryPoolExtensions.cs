using System;
using System.Buffers;

namespace Couchbase.Utils
{
    /// <summary>
    /// Extensions of <see cref="MemoryPool{T}"/>.
    /// </summary>
    internal static class MemoryPoolExtensions
    {
        /// <summary>
        /// Rents a block of memory of a specific length.
        /// </summary>
        /// <typeparam name="T">The type of the objects that are in the memory.</typeparam>
        /// <param name="memoryPool">The <see cref="MemoryPool{T}"/>.</param>
        /// <param name="length">Amount of memory requested.</param>
        /// <returns>The block of memory. The caller is responsible for releasing this memory when it is no longer in use.</returns>
        /// <remarks>
        /// The normal implementation of <see cref="MemoryPool{T}.Rent"/> may return more memory than requested.
        /// This method will reduce the size of the returned memory if more is returned than requested.
        /// Note that a greater amount of memory may still be reserved, it is just unused.
        /// </remarks>
        public static SlicedMemoryOwner<T> RentAndSlice<T>(this MemoryPool<T> memoryPool, int length)
        {
            var memoryOwner = memoryPool.Rent(length);

            return new SlicedMemoryOwner<T>(memoryOwner, 0, length);
        }
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
