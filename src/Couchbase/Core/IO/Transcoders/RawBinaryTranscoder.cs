using System;
using System.IO;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.IO.Transcoders
{
    public class RawBinaryTranscoder : BaseTranscoder
    {
        public override Flags GetFormat<T>(T value)
        {
            var typeCode = Type.GetTypeCode(typeof(T));
            if (typeof(T) == typeof(byte[]))
            {
                var dataFormat = DataFormat.Binary;
                return new Flags { Compression = Compression.None, DataFormat = dataFormat, TypeCode = typeCode };
            }

            throw new InvalidOperationException("The RawBinaryTranscoder only supports byte arrays as input.");
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            if(value is byte[] bytes && flags.DataFormat == DataFormat.Binary)
            {
                stream.Write(bytes, 0, bytes.Length);
                return;
            }

            throw new InvalidOperationException("The RawBinaryTranscoder can only encode byte arrays.");
        }

        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            if (flags.DataFormat == DataFormat.Binary)
            {
                object value = DecodeBinary(buffer.Span);
                return (T) value;
            }

            throw new InvalidOperationException("The RawBinaryTranscoder can only decode byte arrays.");
        }
    }
}
