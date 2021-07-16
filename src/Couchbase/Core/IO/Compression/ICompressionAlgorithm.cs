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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
