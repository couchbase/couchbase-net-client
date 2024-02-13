#if !NET6_0_OR_GREATER

using System;
using System.Buffers;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// Memory pool for operation responses on legacy runtimes.
    /// </summary>
    internal sealed class OperationResponseMemoryPool : MemoryPool<byte>
    {
        public static OperationResponseMemoryPool Instance { get; } = new();

        public override int MaxBufferSize => int.MaxValue;

        public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
        {
            if (minBufferSize == -1)
            {
                minBufferSize = 4096; // Same as MemoryPool<byte>.Shared
            }
            else if ((uint)minBufferSize > (uint)int.MaxValue)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(minBufferSize));
            }

            return new OperationResponseMemoryPoolBuffer(minBufferSize);
        }

        protected override void Dispose(bool disposing)
        {
        }

        private sealed class OperationResponseMemoryPoolBuffer(int size) : IMemoryOwner<byte>
        {
            private byte[]? _array = OperationResponseArrayPool.Instance.Rent(size);

            public Memory<byte> Memory => _array;

            public void Dispose()
            {
                if (_array != null)
                {
                    OperationResponseArrayPool.Instance.Return(_array);
                    _array = null;
                }
            }
        }
    }
}

#endif
