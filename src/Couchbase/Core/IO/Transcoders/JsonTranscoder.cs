using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Transcoders
{
    public class JsonTranscoder : BaseTranscoder
    {
        [RequiresUnreferencedCode(DefaultSerializer.UnreferencedCodeMessage)]
        [RequiresDynamicCode(DefaultSerializer.RequiresDynamicCodeMessage)]
        public JsonTranscoder()
            : this(DefaultSerializer.Instance)
        {
        }

        public JsonTranscoder(ITypeSerializer serializer)
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
                   if (typeof(T) == typeof(byte[]))
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
                case TypeCode.Char:
                case TypeCode.String:
                case TypeCode.Empty:
                    dataFormat = DataFormat.Json;
                    break;
                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                    return default; //unreachable
            }
            return new Flags { Compression = Operations.Compression.None, DataFormat = dataFormat, TypeCode = typeCode };
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            switch (flags.DataFormat)
            {
                case DataFormat.Reserved:
                case DataFormat.Private:
                case DataFormat.String:
                case DataFormat.Json:
                    SerializeAsJson(stream, value);
                    break;

                case DataFormat.Binary:
                    if (value is byte[] bytes)
                    {
                        if (opcode == OpCode.Append || opcode == OpCode.Prepend)
                        {
                            stream.Write(bytes, 0, bytes.Length);
                            break;
                        }
                        ThrowHelper.ThrowUnsupportedException("JsonTranscoder does not support byte arrays.");
                    }
                    else
                    {
                        var msg = $"The value of T does not match the DataFormat provided: {flags.DataFormat}";
                        ThrowHelper.ThrowArgumentException(msg, nameof(value));
                    }

                    break;

                default:
                    ThrowHelper.ThrowArgumentOutOfRangeException();
                    break;
            }
        }

        [return: MaybeNull]
        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            if (typeof(T) == typeof(byte[]))
            {
                if (opcode is OpCode.Append or OpCode.Prepend)
                {
                    var value = DecodeBinary(buffer.Span);
                    return (T)(object) value;
                }

                ThrowHelper.ThrowUnsupportedException("JsonTranscoder does not support byte arrays.");
                return default!; //unreachable
            }

            //special case for some binary ops
            if (typeof(T) == typeof(ulong) && opcode is OpCode.Increment or OpCode.Decrement)
            {
                var value = ByteConverter.ToUInt64(buffer.Span, true);
                return (T)(object)value;
            }

            //everything else gets the JSON treatment
            return DeserializeAsJson<T>(buffer);
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
