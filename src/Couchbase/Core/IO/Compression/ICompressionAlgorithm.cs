using System;
using System.Buffers;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Core.IO.Compression
{
    /// <summary>
    /// Interface for an implementation of a compression algorithm used to compress/decompress request and response bodies for key/value operations.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public interface ICompressionAlgorithm
    {
        /// <summary>
        /// Compression algorithm implemented by this class.
        /// </summary>
        CompressionAlgorithm Algorithm { get; }

        /// <summary>
        /// Compresses an input buffer.
        /// </summary>
        /// <param name="input">Buffer to compress.</param>
        /// <returns>
        /// A compressed buffer. Ownership of the buffer is passed to the caller.
        /// </returns>
        IMemoryOwner<byte> Compress(ReadOnlyMemory<byte> input);

        /// <summary>
        /// Decompresses an input buffer.
        /// </summary>
        /// <param name="input">Buffer to compress.</param>
        /// <returns>A compressed buffer. Ownership of the buffer is passed to the caller.</returns>
        /// <remarks>
        /// May throw an exception if the input buffer is not valid.
        /// </remarks>
        IMemoryOwner<byte> Decompress(ReadOnlyMemory<byte> input);
    }
}
