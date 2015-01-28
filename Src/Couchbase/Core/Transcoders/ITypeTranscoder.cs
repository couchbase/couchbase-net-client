using System;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Transcoders
{
    public interface ITypeTranscoder
    {
        byte[] Encode<T>(T value, Flags flags);

        T Decode<T>(ArraySegment<byte> buffer, int offset, int length, Flags flags);

        T Decode<T>(byte[] buffer, int offset, int length, Flags flags);
    }
}
