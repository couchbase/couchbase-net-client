#if !NET6_0_OR_GREATER

using System.Buffers;

namespace Couchbase.Utils
{
    /// <summary>
    /// Array pool for operation responses on legacy runtimes.
    /// </summary>
    internal sealed class OperationResponseArrayPool : ArrayPool<byte>
    {
        // For arrays up to this size, use the shared pool from .NET. 1MB is the largest size that the shared pool
        // retains in the current implementation for .NET 4.
        private const int SharedPoolMaxArrayLength = 1024 * 1024;

        // An inner pool that allows larger arrays (up to 32MB) and a maximum of 10 arrays of each size.
        // The inner implementation for .NET 4 a (as of this writing) is buckets of exponentially increasing sizes.
        // Since we start with arrays > 1MB, the buckets uses should be 2MB, 4MB, 8MB, 16MB, 32MB,
        // each retaining up to 10 arrays. 32MB is the chosen max because it's the next step that's greater than the maximum
        // Couchbase document size of 20MB. Note that these pools do not scale down due to non-use or memory pressure
        // in the current backing implementation.
        private static readonly ArrayPool<byte> LargeBufferPool = Create(32 * 1024 * 1024 /* 32 MB */, 10);

        public static OperationResponseArrayPool Instance { get; } = new();

        private OperationResponseArrayPool()
        {
        }

        public override byte[] Rent(int minimumLength) =>
            minimumLength <= SharedPoolMaxArrayLength
                ? Shared.Rent(minimumLength)
                : LargeBufferPool.Rent(minimumLength);

        public override void Return(byte[] array, bool clearArray = false)
        {
            if (array is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(array));
            }

            if (array.Length <= SharedPoolMaxArrayLength)
            {
                Shared.Return(array, clearArray);
            }
            else
            {
                LargeBufferPool.Return(array, clearArray);
            }
        }
    }
}

#endif
