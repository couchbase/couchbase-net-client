using System;
using System.IO;
using System.Text;
using Common.Logging;
using Couchbase.IO.Operations;
using Newtonsoft.Json;

namespace Couchbase.Core.Serializers
{
    internal sealed class TypeSerializer : ITypeSerializer
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private static readonly byte[] NullArray = new byte[0];

        public byte[] Serialize<T>(OperationBase<T> operation)
        {
            var value = (object)operation.RawValue;
            var type = typeof(T);
            var typeCode = value == null ?
                TypeCode.DBNull :
                Type.GetTypeCode(type);

            byte[] bytes = null;
            switch (typeCode)
            {
                case TypeCode.String:
                    bytes = GetBytes(value as string);
                    break;
                case TypeCode.Int32:
                    bytes = GetBytes(Convert.ToInt32(value));
                    break;
                case TypeCode.DBNull:
                    bytes = GetBytes();
                    break;
            }
            return bytes;
        }

        public T Deserialize<T>(OperationBase<T> operation)
        {
            var type = typeof(T);
            var typeCode = Type.GetTypeCode(type);
            var operationBody = operation.Body;
            var data = operationBody.Data;
            var bodyLength = operation.Header.BodyLength;
            var extrasLength = operation.Header.ExtrasLength;
            const int headerLength = OperationBase<T>.HeaderLength;

            object value = null;
            switch (typeCode)
            {
                case TypeCode.String:
                    value = GetString(data, headerLength + extrasLength, bodyLength - extrasLength);
                    break;
                case TypeCode.Int32:
                    value = GetInt32(data);
                    break;
                default:
                    value = Deserialize<T>(data, headerLength + extrasLength, bodyLength - extrasLength);
                    break;
            }
            return (T)value;
        }

        public T Deserialize<T>(ArraySegment<byte> bytes, int offset, int length)
        {
            //It would be better to do this without converting to a string first - a TODO
            var value = GetString(bytes, offset, length);

            Log.Trace(value);
            return JsonConvert.DeserializeObject<T>(value);
        }

        static byte[] GetBytes()
        {
            return NullArray;
        }

        static byte[] GetBytes(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        static byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(value);
        }

        private static string GetString(ArraySegment<byte> bytes, int offset, int length)
        {
            var result = string.Empty;
            if (bytes.Array != null)
            {
                result = Encoding.UTF8.GetString(bytes.Array, offset, length);
            }
            return result;
        }

        static string GetString(ArraySegment<byte> bytes)
        {
            var result = string.Empty;
            if (bytes.Array != null)
            {
                var index = bytes.Offset;
                var count = bytes.Array.Length - bytes.Offset;
                result = Encoding.UTF8.GetString(bytes.Array, index, count);
            }
            return result;
        }

        static int GetInt32(ArraySegment<byte> bytes)
        {
            var result = 0;
            if (bytes.Array != null)
            {
                result = BitConverter.ToInt32(bytes.Array, bytes.Offset);
            }
            return result;
        }
    }
}
