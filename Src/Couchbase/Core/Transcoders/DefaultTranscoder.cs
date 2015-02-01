﻿using Common.Logging;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using System.Text;

namespace Couchbase.Core.Transcoders
{
    public class DefaultTranscoder : ITypeTranscoder
    {
        private static readonly ILog Log = LogManager.GetLogger<DefaultTranscoder>();
        private readonly IByteConverter _converter;
        private readonly JsonSerializerSettings _outgoingSerializerSettings;
        private readonly JsonSerializerSettings _incomingSerializerSettings;

        public DefaultTranscoder(IByteConverter converter)
            : this(converter, new JsonSerializerSettings(), new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() })
        {
        }

        public DefaultTranscoder(IByteConverter converter, JsonSerializerSettings incomingSerializerSettings, JsonSerializerSettings outgoingSerializerSettings)
        {
            _converter = converter;
            _incomingSerializerSettings = incomingSerializerSettings;
            _outgoingSerializerSettings = outgoingSerializerSettings;
        }

        public byte[] Encode<T>(T value, Flags flags)
        {
            byte[] bytes;
            switch (flags.DataFormat)
            {
                case DataFormat.Reserved:
                case DataFormat.Private:
                    bytes = Encode(value);
                    break;

                case DataFormat.Json:
                    bytes = SerializeAsJson(value);
                    break;

                case DataFormat.Binary:
                    if (typeof(T) == typeof(byte[]))
                    {
                        bytes = value as byte[];
                    }
                    else
                    {
                        var msg = string.Format("The value of T does not match the DataFormat provided: {0}",
                            flags.DataFormat);
                        throw new ArgumentException(msg);
                    }
                    break;

                case DataFormat.String:
                    bytes = Encode(value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            return bytes;
        }

        public byte[] Encode<T>(T value)
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

        public T Decode<T>(byte[] buffer, int offset, int length, Flags flags)
        {
            object value = default(T);
            switch (flags.DataFormat)
            {
                case DataFormat.Reserved:
                case DataFormat.Private:
                    if (typeof (T) == typeof (byte[]))
                    {
                        value = DecodeBinary(buffer, offset, length);
                    }
                    else
                    {
                        value = Decode<T>(buffer, offset, length);
                    }
                    break;

                case DataFormat.Json:
                    if (typeof (T) == typeof (string))
                    {
                        value = Decode(buffer, offset, length);
                    }
                    else
                    {
                        value = DeserializeAsJson<T>(buffer, offset, length);
                    }
                    break;

                case DataFormat.Binary:
                    if (typeof(T) == typeof(byte[]))
                    {
                        value = DecodeBinary(buffer, offset, length);
                    }
                    else
                    {
                        var msg = string.Format("The value of T does not match the DataFormat provided: {0}",
                            flags.DataFormat);
                        throw new ArgumentException(msg);
                    }
                    break;

                case DataFormat.String:
                    value = Decode(buffer, offset, length);
                    break;

                default:
                    value = Decode(buffer, offset, length);
                    break;
            }
            return (T)value;
        }

        public T Decode<T>(byte[] buffer, int offset, int length)
        {
            object value = default(T);

            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                case TypeCode.Empty:
                case TypeCode.DBNull:
                case TypeCode.String:
                case TypeCode.Char:
                    value = Decode(buffer, offset, length);
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
            T value;
            using (var ms = new MemoryStream(buffer, offset, length))
            {
                using (var sr = new StreamReader(ms))
                {
                    using (var jr = new JsonTextReader(sr))
                    {
                        var serializer = JsonSerializer.Create(_incomingSerializerSettings);

                        //use the following code block only for value types
                        //strangely enough Nullable<T> itself is a value type so we need to filter it out
                        if (typeof(T).IsValueType && (!typeof(T).IsGenericType || typeof(T).GetGenericTypeDefinition() != typeof(Nullable<>)))
                        {
                            //we can't declare Nullable<T> because T is not restricted to struct in this method scope
                            object nullableVal = serializer.Deserialize(jr, typeof(Nullable<>).MakeGenericType(typeof(T)));
                            //either we have a null or an instance of Nullabte<T> that can be cast directly to T
                            value = nullableVal == null ? default(T) : (T)nullableVal;
                        }
                        else
                        {
                            value = serializer.Deserialize<T>(jr);
                        }
                    }
                }
            }
            return value;
        }

        public T Decode<T>(ArraySegment<byte> buffer, int offset, int length, Flags flags)
        {
            return Decode<T>(buffer.Array, offset, length, flags);
        }

        public byte[] SerializeAsJson(object value)
        {
            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms))
                {
                    using (var jr = new JsonTextWriter(sw))
                    {
                        var serializer = JsonSerializer.Create(_outgoingSerializerSettings);
                        serializer.Serialize(jr, value);
                    }
                }
                return ms.ToArray();
            }
        }

        private string Decode(byte[] buffer, int offset, int length)
        {
            var result = string.Empty;
            if (buffer != null && buffer.Length > 0 && length > 0)
            {
                result = Encoding.UTF8.GetString(buffer, offset, length);
            }
            return result;
        }

        private byte[] DecodeBinary(byte[] buffer, int offset, int length)
        {
            var temp = new byte[length];
            Buffer.BlockCopy(buffer, offset, temp, 0, length);
            return temp;
        }
    }
}