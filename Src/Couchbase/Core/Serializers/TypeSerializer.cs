using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Common.Logging;
using Couchbase.IO.Operations;
using Newtonsoft.Json;
using System;
using System.Text;

namespace Couchbase.Core.Serializers
{
    internal sealed class TypeSerializer : ITypeSerializer
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
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
                default:
                    bytes = GetBytes2(value);
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
                    value = Deserialize(data, headerLength + extrasLength, bodyLength - extrasLength);
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
            var value = Deserialize(bytes, offset, length);

            Log.Trace(value);
            return JsonConvert.DeserializeObject<T>(value);
        }

        public string Deserialize(ArraySegment<byte> bytes, int offset, int length)
        {
            var result = string.Empty;
            if (bytes.Array != null)
            {
                result = Encoding.UTF8.GetString(bytes.Array, offset, length);
            }
            return result;
        }

        private static byte[] GetBytes<T>(T value)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, value);
                return ms.GetBuffer();
            }
        }

        private static byte[] GetBytes2<T>(T value)
        {
            var obj  = JsonConvert.SerializeObject(value);
            return Encoding.UTF8.GetBytes(obj);
        }

        private static byte[] GetBytes()
        {
            return NullArray;
        }

        private static byte[] GetBytes(string value)
        {
            return Encoding.UTF8.GetBytes(value);
        }

        private static byte[] GetBytes(int value)
        {
            return BitConverter.GetBytes(value);
        }

        private static int GetInt32(ArraySegment<byte> bytes)
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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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