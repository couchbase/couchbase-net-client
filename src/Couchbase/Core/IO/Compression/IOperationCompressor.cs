using System;
using System.Buffers;

#nullable enable

namespace Couchbase.Core.IO.Compression
{
    /// <summary>
    /// Compresses or decompresses the body of an operation.
    /// </summary>
    internal interface IOperationCompressor
    {
        /// <summary>
        /// Compresses the body of an operation.
        /// </summary>
        /// <param name="input">Buffer to compress.</param>
        /// <returns>
        /// A compressed buffer. Ownership of the buffer is passed to the caller.
        /// Should return null if compression doesn't meet the configured compression rules, such as minimum size or ratio.
        /// </returns>
        IMemoryOwner<byte>? Compress(ReadOnlyMemory<byte> input);

        /// <summary>
        /// Decompresses the body of an operation.
        /// </summary>
        /// <param name="input">Buffer to compress.</param>
        /// <returns>A compressed buffer. Ownership of the buffer is passed to the caller.</returns>
        /// <remarks>
        /// May throw an exception if the input buffer is not valid.
        /// </remarks>
        IMemoryOwner<byte> Decompress(ReadOnlyMemory<byte> input);
    }
}
