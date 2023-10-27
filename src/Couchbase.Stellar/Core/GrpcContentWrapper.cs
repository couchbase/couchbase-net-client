using Couchbase.Core.IO.Serializers;
using Google.Protobuf;

namespace Couchbase.Stellar.Core;

internal record GrpcContentWrapper(ByteString Content, uint ContentFlags, ITypeSerializer Serializer)
{
    public T? ContentAs<T>()
    {
        // TODO: what to do with ContentFlags?
        return Serializer.Deserialize<T>(Content.Memory);
    }
}
