using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;
using ByteConverter = Couchbase.Core.IO.Converters.ByteConverter;

#nullable enable

namespace Couchbase.Core.IO.Transcoders
{
     /// <summary>
    /// Provides the legacy implementation for <see cref="ITypeTranscoder"/> interface that matches sdk2 behavior.
    /// </summary>
    public class LegacyTranscoder : BaseTranscoder
     {
        public LegacyTranscoder()
            : this(DefaultSerializer.Instance)
        {
        }

        public LegacyTranscoder(ITypeSerializer serializer)
        {
            Serializer = serializer;
        }

        public override Flags GetFormat<T>(T value)
        {
            var dataFormat = DataFormat.Json;
            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                case TypeCode.Object:
                    if (typeof(T) == typeof(Byte[]))
                    {
                        dataFormat = DataFormat.Binary;
                    }
                    break;
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                    dataFormat = DataFormat.Json;
                    break;
                case TypeCode.Char:
                case TypeCode.String:
                    dataFormat = DataFormat.String;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return new Flags() { Compression = Operations.Compression.None, DataFormat = dataFormat, TypeCode = typeCode };
        }

        /// <inheritdoc />
        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            switch (flags.DataFormat)
            {
                case DataFormat.Reserved:
                case DataFormat.Private:
                case DataFormat.String:
                    Encode(stream, value, flags.TypeCode, opcode);
                    break;

                case DataFormat.Json:
                    SerializeAsJson(stream, value);
                    break;

                case DataFormat.Binary:
                    if (value is byte[] bytes)
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        var msg = $"The value of T does not match the DataFormat provided: {flags.DataFormat}";
                        throw new ArgumentException(msg);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Encodes the specified value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream to receive the encoded value.</param>
        /// <param name="value">The value.</param>
        /// <param name="typeCode">Type to use for encoding</param>
        /// <param name="opcode"></param>
        /// <exception cref="InvalidEnumArgumentException">Invalid typeCode.</exception>
        public virtual void Encode<T>(Stream stream, T value, TypeCode typeCode, OpCode opcode)
        {
            switch (typeCode)
            {
                case TypeCode.Empty:
                case TypeCode.String:
                case TypeCode.Char:
                    var str = Convert.ToString(value);
                    using (var bufferOwner = MemoryPool<byte>.Shared.Rent(ByteConverter.GetStringByteCount(str)))
                    {
                        var length = ByteConverter.FromString(str, bufferOwner.Memory.Span);
                        stream.Write(bufferOwner.Memory.Slice(0, length));
                    }
                    break;

                case TypeCode.Int16:
                {
                    Span<byte> bytes = stackalloc byte[sizeof(short)];
                    ByteConverter.FromInt16(Convert.ToInt16(value), bytes, false);
                    WriteHelper(stream, bytes);
                    break;
                }

                case TypeCode.UInt16:
                {
                    Span<byte> bytes = stackalloc byte[sizeof(ushort)];
                    ByteConverter.FromUInt16(Convert.ToUInt16(value), bytes, false);
                    WriteHelper(stream, bytes);
                    break;
                }

                case TypeCode.Int32:
                {
                    Span<byte> bytes = stackalloc byte[sizeof(int)];
                    ByteConverter.FromInt32(Convert.ToInt32(value), bytes, false);
                    WriteHelper(stream, bytes);
                    break;
                }

                case TypeCode.UInt32:
                {
                    Span<byte> bytes = stackalloc byte[sizeof(uint)];
                    ByteConverter.FromUInt32(Convert.ToUInt32(value), bytes, false);
                    WriteHelper(stream, bytes);
                    break;
                }

                case TypeCode.Int64:
                {
                    Span<byte> bytes = stackalloc byte[sizeof(long)];
                    ByteConverter.FromInt64(Convert.ToInt64(value), bytes, false);
                    WriteHelper(stream, bytes);
                    break;
                }

                case TypeCode.UInt64:
                {
                    Span<byte> bytes = stackalloc byte[sizeof(ulong)];
                    if (opcode == OpCode.Increment || opcode == OpCode.Decrement)
                    {
                        ByteConverter.FromUInt64(Convert.ToUInt64(value), bytes, true);
                    }
                    else
                    {
                        ByteConverter.FromUInt64(Convert.ToUInt64(value), bytes, false);
                    }
                    WriteHelper(stream, bytes);
                    break;
                }

                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Object:
                    SerializeAsJson(stream, value);
                    break;

                default:
                    throw new InvalidEnumArgumentException(nameof(typeCode), (int) typeCode, typeof(TypeCode));
            }
        }

        /// <inheritdoc />
        [return: MaybeNull]
        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            object? value;
            switch (flags.DataFormat)
            {
                case DataFormat.Reserved:
                case DataFormat.Private:
                    if (typeof (T) == typeof (byte[]))
                    {
                        value = DecodeBinary(buffer.Span);
                    }
                    else
                    {
                        value = Decode<T>(buffer, opcode);
                    }
                    break;

                case DataFormat.Json:
                    if (typeof (T) == typeof (string))
                    {
                        value = DecodeString(buffer.Span);
                    }
                    else
                    {
                        value = DeserializeAsJson<T>(buffer);
                    }
                    break;

                case DataFormat.Binary:
                    if (typeof(T) == typeof(byte[]))
                    {
                        value = DecodeBinary(buffer.Span);
                    }
                    else
                    {
                        var msg = $"The value of T does not match the DataFormat provided: {flags.DataFormat}";
                        throw new ArgumentException(msg);
                    }
                    break;

                case DataFormat.String:
                    if (typeof(T) == typeof(char))
                    {
                        value = DecodeChar(buffer.Span);
                    }
                    else
                    {
                        value = DecodeString(buffer.Span);
                    }
                    break;

                default:
                    value = DecodeString(buffer.Span);
                    break;
            }
            return (T?)value;
        }

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer.</param>
        /// <param name="opcode">The opcode of the operation.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public virtual T? Decode<T>(ReadOnlyMemory<byte> buffer, OpCode opcode)
        {
            object? value = default(T);

            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                case TypeCode.Empty:
                case TypeCode.String:
                    value = DecodeString(buffer.Span);
                    break;

                case TypeCode.Char:
                    value = DecodeChar(buffer.Span);
                    break;

                case TypeCode.Int16:
                    if (buffer.Length > 0)
                    {
                        value = ByteConverter.ToInt16(buffer.Span, false);
                    }
                    break;

                case TypeCode.UInt16:
                    if (buffer.Length > 0)
                    {
                        value = ByteConverter.ToUInt16(buffer.Span, false);
                    }
                    break;

                case TypeCode.Int32:
                    if (buffer.Length > 0)
                    {
                        value = ByteConverter.ToInt32(buffer.Span, false);
                    }
                    break;

                case TypeCode.UInt32:
                    if (buffer.Length > 0)
                    {
                        value = ByteConverter.ToUInt32(buffer.Span, false);
                    }
                    break;

                case TypeCode.Int64:
                    if (buffer.Length > 0)
                    {
                        value = ByteConverter.ToInt64(buffer.Span, false);
                    }
                    break;

                case TypeCode.UInt64:
                    if (buffer.Length > 0)
                    {
                        if (opcode == OpCode.Increment || opcode == OpCode.Decrement)
                        {
                            value = ByteConverter.ToUInt64(buffer.Span, true);
                        }
                        else
                        {
                            value = ByteConverter.ToUInt64(buffer.Span, false);
                        }
                    }
                    break;

                case TypeCode.Single:
                    break;

                case TypeCode.Double:
                    break;

                case TypeCode.Decimal:
                    break;

                case TypeCode.DateTime:
                    break;

                case TypeCode.Boolean:
                    break;

                case TypeCode.SByte:
                    break;

                case TypeCode.Byte:
                    break;

                case TypeCode.Object:
                    value = DeserializeAsJson<T>(buffer);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            return (T?)value;
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
