using System;
using Couchbase.Core.Serialization;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Transcoders
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
        /// <param name="value">The value of the key to encode.</param>
        /// <param name="flags">The flags used for decoding the response.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        byte[] Encode<T>(T value, Flags flags, OperationCode opcode);

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer representing the value of the key to decode.</param>
        /// <param name="offset">The offset to start reading at.</param>
        /// <param name="length">The length to read from the buffer.</param>
        /// <param name="flags">The flags used to encode the payload.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        T Decode<T>(ArraySegment<byte> buffer, int offset, int length, Flags flags, OperationCode opcode);

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="flags">The flags used for decoding the payload.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        T Decode<T>(byte[] buffer, int offset, int length, Flags flags, OperationCode opcode);

        /// <summary>
        /// Gets or sets the serializer used by the <see cref="ITypeTranscoder"/> implementation.
        /// </summary>
        ITypeSerializer Serializer { get; set; }

        /// <summary>
        /// Gets or sets the byte converter used by used by the <see cref="ITypeTranscoder"/> implementation.
        /// </summary>
        IByteConverter Converter { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
