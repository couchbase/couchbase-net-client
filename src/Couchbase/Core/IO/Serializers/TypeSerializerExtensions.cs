using System;
using System.IO;

namespace Couchbase.Core.IO.Serializers
{
    public static class TypeSerializerExtensions
    {
        /// <summary>
        /// Deserializes the specified buffer into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="typeSerializer">The <see cref="ITypeSerializer"/>.</param>
        /// <param name="buffer">The buffer to deserialize from.</param>
        /// <param name="offset">The offset of the buffer to start reading from.</param>
        /// <param name="length">The length of the buffer to read from.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        [Obsolete("Use the ReadOnlyMemory<byte> based overload of Deserialize<T>.")]
        public static T Deserialize<T>(this ITypeSerializer typeSerializer, byte[] buffer, int offset, int length)
        {
            return typeSerializer.Deserialize<T>(buffer.AsMemory(offset, length));
        }

        /// <summary>
        /// Serializes the specified object onto a stream.
        /// </summary>
        /// <param name="typeSerializer">The <see cref="ITypeSerializer"/>.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A byte array containing the serialized object.</returns>
        public static byte[] Serialize(this ITypeSerializer typeSerializer, object obj)
        {
            using (var stream = new MemoryStream())
            {
                typeSerializer.Serialize(stream, obj);

                return stream.ToArray();
            }
        }
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
