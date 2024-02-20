using System;
using System.Runtime.CompilerServices;
using System.Text;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// Provides services to apply hash algorithms and map a key to a vBucket ID.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public static class VBucketMapper
    {
        /// <summary>
        /// Calculate the mask used to calculate vBucket IDs based on the number of vBuckets in the cluster.
        /// This is primarily a performance optimization, since this can be calculated once and stored.
        /// </summary>
        /// <param name="vBucketCount">The number of vBuckets in the cluster.</param>
        /// <returns>The mask to use when calculating the vBucket ID.</returns>
        public static short GetMask(int vBucketCount)
        {
            if (vBucketCount < 1 || vBucketCount > 32768)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(vBucketCount));
            }

            return (short) (vBucketCount - 1);
        }

        /// <summary>
        /// Get the vBucketID for a given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mask">The previously calculated mask from a call to <see cref="GetMask"/>.</param>
        /// <returns>The vBucketID for the key.</returns>
        [SkipLocalsInit] // Avoid unnecessary cost of zero-filling keyBytes in Span scenario
        public static short GetVBucketId(string key, short mask)
        {
#if !SPAN_SUPPORT
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
#else
            Span<byte> keyBytes = stackalloc byte[OperationHeader.MaxKeyLength];
            var bytes = Encoding.UTF8.GetBytes(key.AsSpan(), keyBytes);
            keyBytes = keyBytes.Slice(0, bytes);
#endif
            return GetVBucketId(keyBytes, mask);
        }

        /// <summary>
        /// Get the vBucketID for a given key.
        /// </summary>
        /// <param name="keyBytes">The key encoded as UTF-8.</param>
        /// <param name="vBucketCount">The number of vBuckets in the cluster.</param>
        /// <returns>The vBucketID for the key.</returns>
        public static short GetVBucketId(ReadOnlySpan<byte> keyBytes, int vBucketCount) => GetVBucketId(keyBytes, GetMask(vBucketCount));

        /// <summary>
        /// Get the vBucketID for a given key.
        /// </summary>
        /// <param name="keyBytes">The key encoded as UTF-8.</param>
        /// <param name="mask">The previously calculated mask from a call to <see cref="GetMask"/>.</param>
        /// <returns>The vBucketID for the key.</returns>
        public static short GetVBucketId(ReadOnlySpan<byte> keyBytes, short mask)
        {
            var hash = Crc32.ComputeHash(keyBytes);

            return (short) (hash & mask);
        }
    }
}
