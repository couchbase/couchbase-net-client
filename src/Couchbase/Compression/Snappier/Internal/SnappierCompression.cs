using System;
using System.Buffers;
using Couchbase.Core.IO.Compression;
using Snappier;

namespace Couchbase.Compression.Snappier.Internal
{
    /// <summary>
    /// Snappy compression using Snappier.
    /// </summary>
    internal sealed class SnappierCompression : ICompressionAlgorithm
    {
        /// <inheritdoc />
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.Snappy;

        /// <inheritdoc />
        public IMemoryOwner<byte> Compress(ReadOnlyMemory<byte> input) => Snappy.CompressToMemory(input.Span);

        /// <inheritdoc />
        public IMemoryOwner<byte> Decompress(ReadOnlyMemory<byte> input) => Snappy.DecompressToMemory(input.Span);
    }
}
