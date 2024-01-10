#if NETCOREAPP3_1_OR_GREATER
using Couchbase.Core.IO.Serializers;
using Google.Protobuf;

namespace Couchbase.Stellar.Core;

#nullable enable

internal record GrpcContentWrapper(ByteString Content, uint ContentFlags, ITypeSerializer Serializer)
{
    public T? ContentAs<T>()
    {
        // TODO: what to do with ContentFlags?
        return Serializer.Deserialize<T>(Content.Memory);
    }
}
#endif
