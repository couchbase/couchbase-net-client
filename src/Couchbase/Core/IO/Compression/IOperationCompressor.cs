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
