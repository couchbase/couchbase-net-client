// Ported from Couchbase.Extensions.Compression.Snappier so we can include compression/decompression spans

using System;
using System.Buffers;
using Couchbase.Core.IO.Compression;
using Snappier;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests
{
    /// <summary>
    /// Snappy compression using Snappier.
    /// </summary>
    internal class SnappierCompression : ICompressionAlgorithm
    {
        /// <inheritdoc />
        public CompressionAlgorithm Algorithm => CompressionAlgorithm.Snappy;

        /// <inheritdoc />
        public IMemoryOwner<byte> Compress(ReadOnlyMemory<byte> input) => Snappy.CompressToMemory(input.Span);

        /// <inheritdoc />
        public IMemoryOwner<byte> Decompress(ReadOnlyMemory<byte> input) => Snappy.DecompressToMemory(input.Span);
    }
}
