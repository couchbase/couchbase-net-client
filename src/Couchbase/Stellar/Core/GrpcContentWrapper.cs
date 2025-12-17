#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Buffers.Binary;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Google.Protobuf;

namespace Couchbase.Stellar.Core;

#nullable enable

internal record GrpcContentWrapper(ByteString Content, uint ContentFlags, ITypeTranscoder Transcoder, OpCode OpCode = OpCode.Get, bool IsFullDoc = true)
{
    public T? ContentAs<T>()
    {
        if (!IsFullDoc)
            return (Transcoder.Serializer ?? DefaultSerializer.Instance).Deserialize<T>(Content.Memory);

        // A full doc ContentAs we use the Transcoder
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, ContentFlags);
        return Transcoder.Decode<T>(Content.Memory, Flags.Read(buffer), OpCode);
    }
}
#endif
