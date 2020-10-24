using System;
using System.Buffers;

#nullable enable

namespace Couchbase.Core.IO.Compression
{
    /// <summary>
    /// Compression algorithm which does not compress or decompress data.
    /// This is the default if no compression algorithm is registered.
    /// </summary>
    internal class NullCompressionAlgorithm : ICompressionAlgorithm
    {
        /// <inheritdoc />
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.None;

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">No compression algorithm registered.</exception>
        public IMemoryOwner<byte> Compress(ReadOnlyMemory<byte> input) =>
            throw new NotSupportedException("No compression algorithm registered.");

        /// <inheritdoc />
        /// <exception cref="NotSupportedException">No compression algorithm registered.</exception>
        public IMemoryOwner<byte> Decompress(ReadOnlyMemory<byte> input) =>
            throw new NotSupportedException("No compression algorithm registered.");
    }
}
