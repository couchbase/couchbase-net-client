using System;
using System.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.Core.IO.Transcoders
{
    /// <summary>
    /// An interface for providing transcoder implementations.
    /// </summary>
    public interface ITypeTranscoder
    {
        /// <summary>
        /// Get data formatting based on the generic type and/or the actual value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">Value to be formatted.</param>
        /// <returns>Flags used to format value written to operation payload.</returns>
        Flags GetFormat<T>(T value);

        /// <summary>
        /// Encodes the specified value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream to receive the encoded value.</param>
        /// <param name="value">The value of the key to encode.</param>
        /// <param name="flags">The flags used for decoding the response.</param>
        /// <param name="opcode"></param>
        void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode);

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer representing the value of the key to decode.</param>
        /// <param name="flags">The flags used to encode the payload.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        T? Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode);

        /// <summary>
        /// Gets or sets the serializer used by the <see cref="ITypeTranscoder"/> implementation.
        /// </summary>
        ITypeSerializer? Serializer { get; set; }
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
