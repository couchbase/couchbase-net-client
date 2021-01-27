using System;
using System.IO;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.IO.Transcoders
{
    public class JsonTranscoder : BaseTranscoder
    {
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
                    throw new ArgumentOutOfRangeException();
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
                        throw new UnsupportedException("JsonTranscoder does not support byte arrays.");
                    }
                    else
                    {
                        var msg = $"The value of T does not match the DataFormat provided: {flags.DataFormat}";
                        throw new ArgumentException(msg);
                    }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            var typeCode = Type.GetTypeCode(typeof(T));
            if (typeof(T) == typeof(byte[]))
            {
                if (opcode == OpCode.Append || opcode == OpCode.Prepend)
                {
                    object value = DecodeBinary(buffer.Span);
                    return (T) value;
                }
                throw new UnsupportedException("JsonTranscoder does not support byte arrays.");
            }
            //special case for some binary ops
            if (typeCode == TypeCode.UInt64 && (opcode == OpCode.Increment || opcode == OpCode.Decrement))
            {
                object value = ByteConverter.ToUInt64(buffer.Span, true);
                return (T) value;
            }

            //everything else gets the JSON treatment
            return DeserializeAsJson<T>(buffer);
        }
    }
}
