using System;
using System.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.IO.Transcoders
{
    public class RawJsonTranscoder : ITypeTranscoder
    {
        public Flags GetFormat<T>(T value)
        {
            throw new NotImplementedException();
        }

        public void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode)
        {
            throw new NotImplementedException();
        }

        public T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode)
        {
            throw new NotImplementedException();
        }

        public ITypeSerializer Serializer { get; set; }
    }
}
