#if NET45
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Couchbase.Core.Serialization;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Transcoders
{
    /// <summary>
    /// A transcoder which deserializes in binary format and serializes as JSON. This transcoder will expects a 1.3.X SDK client to be writing the data.
    /// </summary>
    /// <seealso cref="Couchbase.Core.Transcoders.DefaultTranscoder" />
    public class BinaryToJsonTranscoder : DefaultTranscoder
    {
        public BinaryToJsonTranscoder()
        {
        }

        public BinaryToJsonTranscoder(IByteConverter converter) : base(converter)
        {
        }

        public BinaryToJsonTranscoder(IByteConverter converter, ITypeSerializer serializer) : base(converter, serializer)
        {
        }

        /// <summary>
        /// Deserializes using <see cref="BinaryFormatter"/> - does NOT deserialize to JSON!!!!
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns></returns>
        public T DeserializeAsBinary<T>(byte[] buffer, int offset, int length)
        {
            if (buffer.Length < offset + length) return default(T);
            using (var ms = new MemoryStream(buffer, offset, length))
            {
                var formatter = new BinaryFormatter();
                return (T)formatter.Deserialize(ms);
            }
        }

        /// <summary>
        /// Decodes the specified buffer using a <see cref="BinaryFormatter"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        public override T Decode<T>(byte[] buffer, int offset, int length, OperationCode opcode)
        {
            object value = default(T);

            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                case TypeCode.DBNull:
                    value = null;
                    break;
                case TypeCode.Empty:
                case TypeCode.String:
                    value = DecodeString(buffer, offset, length);
                    break;

                case TypeCode.Char:
                    value = DecodeChar(buffer, offset, length);
                    break;

                case TypeCode.Int16:
                    if (length > 0)
                    {
                        value = Converter.ToInt16(buffer, offset, false);
                    }
                    break;

                case TypeCode.UInt16:
                    if (length > 0)
                    {
                        value = Converter.ToUInt16(buffer, offset, false);
                    }
                    break;

                case TypeCode.Int32:
                    if (length > 0)
                    {
                        value = Converter.ToInt32(buffer, offset, false);
                    }
                    break;

                case TypeCode.UInt32:
                    if (length > 0)
                    {
                        value = Converter.ToUInt32(buffer, offset, false);
                    }
                    break;

                case TypeCode.Int64:
                    if (length > 0)
                    {
                        value = Converter.ToInt64(buffer, offset, false);
                    }
                    break;

                case TypeCode.UInt64:
                    if (length > 0)
                    {
                        if (opcode == OperationCode.Increment || opcode == OperationCode.Decrement)
                        {
                            value = Converter.ToUInt64(buffer, offset, true);
                        }
                        else
                        {
                            value = Converter.ToUInt64(buffer, offset, false);
                        }
                    }
                    break;

                case TypeCode.Single:
                    value = Converter.ToSingle(buffer, offset, false);
                    break;

                case TypeCode.Double:
                    value = Converter.ToDouble(buffer, offset, false);
                    break;

                case TypeCode.DateTime:
                    value = Converter.ToDateTime(buffer, offset, false);
                    break;

                case TypeCode.Boolean:
                    value = Converter.ToBoolean(buffer, offset, false);
                    break;

                case TypeCode.SByte:
                case TypeCode.Byte:
                    throw new ArgumentException("SByte and Byte are Not supported by 1.x SDK.");

                case TypeCode.Decimal:
                case TypeCode.Object:
                    value = DeserializeAsBinary<T>(buffer, offset, length);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            return (T)value;

        }
    }
}
#endif

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
