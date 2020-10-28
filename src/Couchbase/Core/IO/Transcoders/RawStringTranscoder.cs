using System;
using System.Buffers;
using System.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Transcoders
{
    public class RawStringTranscoder : BaseTranscoder
    {
        public override Flags GetFormat<T>(T value)
        {
            var typeCode = Type.GetTypeCode(typeof(T));
            if (typeCode == TypeCode.Char || typeCode == TypeCode.String)
            {
                var dataFormat = DataFormat.String;
                return new Flags {Compression = Compression.None, DataFormat = dataFormat, TypeCode = typeCode};
            }

            throw new InvalidOperationException("The RawStringTranscoder only supports strings as input.");
        }

        public override void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            if (value is byte[] bytes && flags.DataFormat == DataFormat.String)
            {
                stream.Write(bytes, 0, bytes.Length);
                return;
            }
            if (value is string str && flags.DataFormat == DataFormat.String)
            {
                using var bufferOwner = MemoryPool<byte>.Shared.Rent(ByteConverter.GetStringByteCount(str));
                var length = ByteConverter.FromString(str, bufferOwner.Memory.Span);
                stream.Write(bufferOwner.Memory.Slice(0, length));
                return;
            }

            throw new InvalidOperationException("The RawStringTranscoder can only encode strings.");
        }

        public override T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            var type = typeof(T);
            if (type == typeof(string) && flags.DataFormat == DataFormat.String)
            {
                object value = DecodeString(buffer.Span);
                return (T) value;
            }

            throw new InvalidOperationException("The RawStringTranscoder can only decode strings.");
        }
    }
}
