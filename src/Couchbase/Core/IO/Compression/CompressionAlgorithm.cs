using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Core.IO.Compression
{
    /// <summary>
    /// Indicates a compression algorithm, which must be supported by Couchbase Server.
    /// </summary>
    /// <remarks>
    /// This enumeration is for future-proofing, currently only Snappy is supported.
    /// </remarks>
    [InterfaceStability(Level.Volatile)]
    public enum CompressionAlgorithm
    {
        /// <summary>
        /// Placeholder for a no-op algorithm.
        /// </summary>
        None,

        /// <summary>
        /// Snappy.
        /// </summary>
        Snappy
    }
}
