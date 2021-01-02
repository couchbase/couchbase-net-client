using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// Wraps a <see cref="IMemoryOwner{T}"/> with information to access a subset of the memory.
    /// This is particularly useful when renting memory from a <see cref="MemoryPool{T}"/>,
    /// which may return a larger array of memory than desired.
    /// </summary>
    /// <typeparam name="T">The type of the objects that are in the memory.</typeparam>
    /// <remarks>
    /// Disposing of this object is simply a forwarder to dispose of the owned memory. It assumed
    /// that this type follows the same rules for memory ownership as <see cref="IMemoryOwner{T}"/>.
    /// For example, calls which return this type assume that the caller takes ownership of the returned
    /// value. Calls which accept this type as a parameter assume that the callee takes ownership of
    /// the passed value.
    ///
    /// Failing to dispose of this type may result in memory leaks. Using this object after it is
    /// disposed may result in accessing memory in use for other purposes.
    ///
    /// While this structure does implement <see cref="IMemoryOwner{T}"/>, casting to the interface
    /// should be avoided as it will cause boxing.
    /// </remarks>
    internal readonly struct SlicedMemoryOwner<T> : IMemoryOwner<T>, IEquatable<SlicedMemoryOwner<T>>
    {
        private readonly int _start;
        private readonly int _length;

        /// <summary>
        /// Shortcut for an empty slice.
        /// </summary>
        public static SlicedMemoryOwner<T> Empty => default;

        /// <summary>
        /// The owned block of memory.
        /// </summary>
        public IMemoryOwner<T>? MemoryOwner { get; }

        /// <summary>
        /// Shortcut to get the memory slice.
        /// </summary>
        public Memory<T> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MemoryOwner?.Memory.Slice(_start, _length) ?? Memory<T>.Empty;
        }

        /// <summary>
        /// Returns true if this <see cref="SlicedMemoryOwner{T}"/> is an empty wrapper.
        /// </summary>
        public bool IsEmpty => MemoryOwner == null;

        /// <summary>
        /// Create a new <see cref="SlicedMemoryOwner{T}"/> without slicing.
        /// </summary>
        /// <param name="memoryOwner">The <see cref="IMemoryOwner{T}"/> to slice.</param>
        public SlicedMemoryOwner(IMemoryOwner<T> memoryOwner)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (memoryOwner == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(memoryOwner));
            }

            MemoryOwner = memoryOwner;
            _start = 0;
            _length = memoryOwner.Memory.Length;
        }

        /// <summary>
        /// Create a new <see cref="SlicedMemoryOwner{T}"/>, slicing the memory and taking ownership.
        /// </summary>
        /// <param name="memoryOwner">The <see cref="IMemoryOwner{T}"/> to slice.</param>
        /// <param name="start">Start index of the slice.</param>
        /// <exception cref="ArgumentNullException"><paramref name="memoryOwner"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> is out of bounds.</exception>
        public SlicedMemoryOwner(IMemoryOwner<T> memoryOwner, int start)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (memoryOwner == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(memoryOwner));
            }

            var memory = memoryOwner.Memory;
            if ((uint) start > (uint) memory.Length)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start));
            }

            MemoryOwner = memoryOwner;
            _start = start;
            _length = memory.Length - start;
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
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (memoryOwner == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(memoryOwner));
            }

            var memory = memoryOwner.Memory;
            if ((uint) start > (uint) memory.Length || (uint) length > (uint) (memory.Length - start))
            {
                // No parameter name aligns with behavior of Memory<T>
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }

            MemoryOwner = memoryOwner;
            _start = start;
            _length = length;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            MemoryOwner?.Dispose();
        }

        /// <summary>
        /// Slices the memory further. Ownership of the memory belongs to the new slice, the old
        /// one should be discarded.
        /// </summary>
        /// <param name="start">Starting offset of the slice.</param>
        /// <returns>New slice of memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlicedMemoryOwner<T> Slice(int start)
        {
            if (MemoryOwner != null)
            {
                return new SlicedMemoryOwner<T>(MemoryOwner, _start + start, _length - start);
            }

            if (start > 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(start));
            }

            return default;
        }

        /// <summary>
        /// Slices the memory further. Ownership of the memory belongs to the new slice, the old
        /// one should be discarded.
        /// </summary>
        /// <param name="start">Starting offset of the slice.</param>
        /// <param name="length">Length of the slice.</param>
        /// <returns>New slice of memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SlicedMemoryOwner<T> Slice(int start, int length)
        {
            if (MemoryOwner != null)
            {
                return new SlicedMemoryOwner<T>(MemoryOwner, _start + start, length);
            }

            if (start > 0 || length > 0)
            {
                // No parameter name aligns with behavior of Memory<T>
                ThrowHelper.ThrowArgumentOutOfRangeException();
            }

            return default;
        }

        public bool Equals(SlicedMemoryOwner<T> other) =>
            ReferenceEquals(MemoryOwner, other.MemoryOwner) &&
            _start == other._start &&
            _length == other._length;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override bool Equals(object? obj) => obj is SlicedMemoryOwner<T> other && Equals(other);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public override int GetHashCode()
        {
            unchecked
            {
                if (MemoryOwner == null)
                {
                    return 0;
                }

                var hashCode = MemoryOwner.GetHashCode();
                hashCode = (hashCode * 397) ^ _start;
                hashCode = (hashCode * 397) ^ _length;
                return hashCode;
            }
        }
    }
}
