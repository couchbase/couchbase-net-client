using System.Text;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Couchbase.Core.Serializers
{
    public sealed class TypeSerializer : ITypeSerializer
    {
        private readonly IByteConverter _converter;

        public TypeSerializer(IByteConverter converter)
        {
            _converter = converter;
        }

        public byte[] Serialize<T>(T value)
        {
            var bytes = new byte[] { };
            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                case TypeCode.String:
                case TypeCode.Char:
                    _converter.FromString(Convert.ToString(value), ref bytes, 0);
                    break;

                case TypeCode.Int16:
                    _converter.FromInt16(Convert.ToInt16(value), ref bytes, 0);
                    break;

                case TypeCode.UInt16:
                    _converter.FromUInt16(Convert.ToUInt16(value), ref bytes, 0);
                    break;

                case TypeCode.Int32:
                    _converter.FromInt32(Convert.ToInt32(value), ref bytes, 0);
                    break;

                case TypeCode.UInt32:
                    _converter.FromUInt32(Convert.ToUInt32(value), ref bytes, 0);
                    break;

                case TypeCode.Int64:
                    _converter.FromInt64(Convert.ToInt64(value), ref bytes, 0);
                    break;

                case TypeCode.UInt64:
                    _converter.FromUInt64(Convert.ToUInt64(value), ref bytes, 0);
                    break;

                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Object:
                    bytes = SerializeAsJson(value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            return bytes;
        }

        public T Deserialize<T>(byte[] buffer, int offset, int length)
        {
            object value = default(T);

            var typeCode = Type.GetTypeCode(typeof (T));
            switch (typeCode)
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                case TypeCode.String:
                case TypeCode.Char:
                    value = Deserialize(buffer, offset, length);
                    break;
                case TypeCode.Int16:
                    if (length > 0)
                    {
                        value = _converter.ToInt16(buffer, offset);
                    }
                    break;
                case TypeCode.UInt16:
                    if (length > 0)
                    {
                        value = _converter.ToUInt16(buffer, offset);
                    }
                    break;
                case TypeCode.Int32:
                    if (length > 0)
                    {
                        value = _converter.ToInt32(buffer, offset);
                    }
                    break;
                case TypeCode.UInt32:
                    if (length > 0)
                    {
                        value = _converter.ToUInt32(buffer, offset);
                    }
                    break;
                case TypeCode.Int64:
                    if (length > 0)
                    {
                        value = _converter.ToInt64(buffer, offset);
                    }
                    break;
                case TypeCode.UInt64:
                    if (length > 0)
                    {
                        value = _converter.ToUInt64(buffer, offset);
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
                    value = DeserializeAsJson<T>(buffer, offset, length);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return (T)value;
        }

        public T DeserializeAsJson<T>(byte[] buffer, int offset, int length)
        {
            var value = default(T);
            using (var ms = new MemoryStream(buffer, offset, length))
            {
                using (var sr = new StreamReader(ms))
                {
                    using (var jr = new JsonTextReader(sr))
                    {
                        var serializer = new JsonSerializer();
                        value = serializer.Deserialize<T>(jr);
                    }
                }
            }
            return value;
        }

        public T Deserialize<T>(ArraySegment<byte> buffer, int offset, int length)
        {
            return Deserialize<T>(buffer.Array, offset, length);
        }

        public byte[] SerializeAsJson<T>(T value)
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                {
                    using (var jr = new JsonTextWriter(sw))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(jr, value);
                    }
                }
                return ms.GetBuffer();
            }
        }

        string Deserialize(byte[] buffer, int offset, int length)
        {
            var result = string.Empty;
            if (buffer != null)
            {
                result = Encoding.UTF8.GetString(buffer, offset, length);
            }
            return result;
        }
    }
}