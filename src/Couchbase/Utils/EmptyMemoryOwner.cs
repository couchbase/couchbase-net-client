using System;
using System.Buffers;

namespace Couchbase.Utils
{
    /// <summary>
    /// Fake implementation of <see cref="IMemoryOwner{T}"/> for wrapping an empty block of memory.
    /// </summary>
    /// <typeparam name="T">The type of the objects that are in the memory.</typeparam>
    internal class EmptyMemoryOwner<T> : IMemoryOwner<T>
    {
        private bool _disposed;

        /// <inheritdoc />
        public Memory<T> Memory =>
            !_disposed ? default(Memory<T>) : throw new ObjectDisposedException(nameof(EmptyMemoryOwner<T>));

        /// <summary>
        /// Create a new <see cref="EmptyMemoryOwner{T}"/>.
        /// </summary>
        public EmptyMemoryOwner()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
        }
    }
}
