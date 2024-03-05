using System;
using Couchbase.Compression.Snappier.Internal;
using Couchbase.Utils;

namespace Couchbase.Compression.Snappier
{
    /// <summary>
    /// Extensions for <see cref="ClusterOptions"/>.
    /// </summary>
    public static class SnappierClusterOptionsExtensions
    {
        /// <summary>
        /// Register Snappier as the compression algorithm for key/value operations.
        /// </summary>
        /// <param name="options">The <see cref="ClusterOptions"/>.</param>
        /// <returns>The <see cref="ClusterOptions"/> for method chaining.</returns>
        public static ClusterOptions WithSnappierCompression(this ClusterOptions options)
        {
            if (options == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(options));
            }

            return options.WithCompressionAlgorithm<SnappierCompression>();
        }
    }
}
