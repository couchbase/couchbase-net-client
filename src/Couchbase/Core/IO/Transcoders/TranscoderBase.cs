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
            var temp = new byte[buffer.Length];
            buffer.CopyTo(temp.AsSpan());
            return temp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteHelper(Stream stream, ReadOnlySpan<byte> buffer)
        {
#if NETCOREAPP2_1 || NETSTANDARD2_1
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
