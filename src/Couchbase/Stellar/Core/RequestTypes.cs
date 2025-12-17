// In this file we decorate the GRPC-generated classes with interfaces to remove the need to copy/paste elsewhere

// ReSharper disable CheckNamespace

#if NETCOREAPP3_1_OR_GREATER

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Couchbase.Protostellar.KV.V1;

internal interface IKeySpec
{
    public string Key { get; set; }
    public string BucketName { get; set; }
    public string ScopeName { get; set; }
    public string CollectionName { get; set; }
}

internal interface IContentRequest
{
    public uint ContentFlags { get; set; }
    public ByteString ContentUncompressed { get; set; }
    public DurabilityLevel DurabilityLevel { get; set; }
}

internal interface IExpiryRequest {

    public Timestamp ExpiryTime { get; set; }
    public uint ExpirySecs { get; set; }
}

internal interface ICasRequest
{
    ulong Cas { get; set; }
}

public partial class ExistsRequest : IKeySpec { }
public partial class GetRequest : IKeySpec { }
public partial class GetAndLockRequest : IKeySpec { }
public partial class GetAndTouchRequest : IKeySpec { }
public partial class InsertRequest : IKeySpec, IContentRequest, IExpiryRequest { }
public partial class RemoveRequest : IKeySpec, ICasRequest { }
public partial class ReplaceRequest : IKeySpec, ICasRequest, IContentRequest, IExpiryRequest { }
public partial class TouchRequest : IKeySpec { }
public partial class UnlockRequest : IKeySpec { }
public partial class UpsertRequest : IKeySpec, IContentRequest, IExpiryRequest { }
public partial class LookupInRequest : IKeySpec { }
public partial class MutateInRequest : IKeySpec, IExpiryRequest { }
public partial class GetAllReplicasRequest : IKeySpec { }
#endif
