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
        public static IMemoryOwner<T> RentAndSlice<T>(this MemoryPool<T> memoryPool, int length)
        {
            var memoryOwner = memoryPool.Rent(length);

            if (memoryOwner.Memory.Length == length)
            {
                return memoryOwner;
            }
            else
            {
                return new SlicedMemoryOwner<T>(memoryOwner, 0, length);
            }
        }
    }
}
