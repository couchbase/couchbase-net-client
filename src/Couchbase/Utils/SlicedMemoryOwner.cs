using System;
using System.Buffers;

namespace Couchbase.Utils
{
    /// <summary>
    /// Slices the memory within another <see cref="IMemoryOwner{T}"/>, transferring ownership of the
    /// memory to the new <see cref="SlicedMemoryOwner{T}"/>. This is particularly useful when
    /// renting memory from a <see cref="MemoryPool{T}"/>, which may return a larger array of memory
    /// than desired.
    /// </summary>
    /// <typeparam name="T">The type of the objects that are in the memory.</typeparam>
    /// <remarks>
    /// Ownership of the memory is transferred to this object, disposing it will dispose the
    /// <see cref="IMemoryOwner{T}"/> reference passed to it. If you wish to simply work with a
    /// slice of memory without transferring ownership, use <see cref="Memory{T}.Slice(int, int)"/>.
    /// </remarks>
    internal class SlicedMemoryOwner<T> : IMemoryOwner<T>
    {
        private readonly IMemoryOwner<T> _memoryOwner;
        private readonly int _start;
        private readonly int _length;

        /// <inheritdoc />
        public Memory<T> Memory => _memoryOwner.Memory.Slice(_start, _length);

        /// <summary>
        /// Create a new <see cref="SlicedMemoryOwner{T}"/>, slicing the memory and taking ownership.
        /// </summary>
        /// <param name="memoryOwner">The <see cref="IMemoryOwner{T}"/> to slice.</param>
        /// <param name="start">Start index of the slice.</param>
        /// <exception cref="ArgumentNullException"><paramref name="memoryOwner"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is out of bounds.</exception>
        public SlicedMemoryOwner(IMemoryOwner<T> memoryOwner, int start)
            : this(memoryOwner, start, (memoryOwner?.Memory.Length ?? 0) - start)
        {
        }

        /// <summary>
        /// Create a new <see cref="SlicedMemoryOwner{T}"/>, slicing the memory and taking ownership.
        /// </summary>
        /// <param name="memoryOwner">The <see cref="IMemoryOwner{T}"/> to slice.</param>
        /// <param name="start">Start index of the slice.</param>
        /// <param name="length">Length of the slice.</param>
        /// <exception cref="ArgumentNullException"><paramref name="memoryOwner"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> or <paramref name="length"/> is out of bounds.</exception>
        public SlicedMemoryOwner(IMemoryOwner<T> memoryOwner, int start, int length)
        {
            _memoryOwner = memoryOwner ?? throw new ArgumentNullException(nameof(memoryOwner));

            if (start < 0 || start >= memoryOwner.Memory.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (length < 0 || start + length > memoryOwner.Memory.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            _start = start;
            _length = length;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _memoryOwner.Dispose();
        }
    }
}
