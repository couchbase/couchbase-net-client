using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.IO.Transcoders
{
    public abstract class BaseTranscoder : ITypeTranscoder
    {
        public abstract Flags GetFormat<T>(T value);

        public abstract void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode);

        public abstract T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode);

        public ITypeSerializer Serializer { get; set; }

        /// <summary>
        /// Deserializes as json.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public virtual T DeserializeAsJson<T>(ReadOnlyMemory<byte> buffer)
        {
            return Serializer.Deserialize<T>(buffer);
        }

        /// <summary>
        /// Serializes as json.
        /// </summary>
        /// <param name="stream">The stream to receive the encoded value.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public void SerializeAsJson(Stream stream, object value)
        {
            Serializer.Serialize(stream, value);
        }

        /// <summary>
        /// Decodes the specified buffer as string.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        protected string DecodeString(ReadOnlySpan<byte> buffer)
        {
            string result = null;
            if (buffer.Length > 0)
            {
                result = ByteConverter.ToString(buffer);
            }
            return result;
        }

        /// <summary>
        /// Decodes the specified buffer as char.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        protected char DecodeChar(ReadOnlySpan<byte> buffer)
        {
            char result = default(char);
            if (buffer.Length > 0)
            {
                var str = ByteConverter.ToString(buffer);
                if (str.Length == 1)
                {
                    result = str[0];
                }
                else if (str.Length > 1)
                {
                    var msg = $"Can not convert string \"{str}\" to char";
                    throw new InvalidCastException(msg);
                }
            }
            return result;
        }

        /// <summary>
        /// Decodes the binary.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        protected byte[] DecodeBinary(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var temp = new byte[buffer.Length];
            buffer.CopyTo(temp.AsSpan());
            return temp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteHelper(Stream stream, ReadOnlySpan<byte> buffer)
        {
#if SPAN_SUPPORT
            stream.Write(buffer);
#else
            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(array);

                stream.Write(array, 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
#endif
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
