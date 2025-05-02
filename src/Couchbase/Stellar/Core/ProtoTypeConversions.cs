#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Analytics;
using Couchbase.Core.IO.Serializers;
using Couchbase.Management.Buckets;
using Couchbase.Protostellar.Admin.Bucket.V1;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Protostellar.Search.V1;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Stellar.KeyValue;
using Google.Protobuf;
using Google.Protobuf.Collections;
using BucketType = Couchbase.Protostellar.Admin.Bucket.V1.BucketType;
using CompressionMode = Couchbase.Protostellar.Admin.Bucket.V1.CompressionMode;
using ConflictResolutionType = Couchbase.Protostellar.Admin.Bucket.V1.ConflictResolutionType;
using CoreKv = Couchbase.KeyValue;
using CoreQuery = Couchbase.Query;
using CoreOpCode = Couchbase.Core.IO.Operations.OpCode;
using DateRange = Couchbase.Protostellar.Search.V1.DateRange;
using DateRangeFacet = Couchbase.Search.DateRangeFacet;
using MatchQuery = Couchbase.Protostellar.Search.V1.MatchQuery;
using NumericRangeFacet = Couchbase.Search.NumericRangeFacet;
using ProtoKv = Couchbase.Protostellar.KV.V1;
using ProtoQuery = Couchbase.Protostellar.Query.V1;
using ProtoLookupInOpCode = Couchbase.Protostellar.KV.V1.LookupInRequest.Types.Spec.Types.Operation;
using ProtoLookupInFlags = Couchbase.Protostellar.KV.V1.LookupInRequest.Types.Spec.Types.Flags;
using ProtoMutateInOpCode = Couchbase.Protostellar.KV.V1.MutateInRequest.Types.Spec.Types.Operation;
using ProtoMutateInFlags = Couchbase.Protostellar.KV.V1.MutateInRequest.Types.Spec.Types.Flags;
using StorageBackend = Couchbase.Protostellar.Admin.Bucket.V1.StorageBackend;
using ProtoFacet = Couchbase.Protostellar.Search.V1.Facet;
using TermFacet = Couchbase.Search.TermFacet;
using NumericRange = Couchbase.Protostellar.Search.V1.NumericRange;

namespace Couchbase.Stellar.Core;

#nullable enable

internal static class TypeConversionExtensions
{
    public static ProtoKv.DurabilityLevel? ToProto(this CoreKv.DurabilityLevel durabilityLevel) =>
        durabilityLevel switch
        {
            CoreKv.DurabilityLevel.None => null,
            CoreKv.DurabilityLevel.Majority => ProtoKv.DurabilityLevel.Majority,
            CoreKv.DurabilityLevel.PersistToMajority => ProtoKv.DurabilityLevel.PersistToMajority,
            CoreKv.DurabilityLevel.MajorityAndPersistToActive => ProtoKv.DurabilityLevel.MajorityAndPersistToActive,
            _ => throw new ArgumentOutOfRangeException(
                $"{nameof(CoreKv.DurabilityLevel)} '{durabilityLevel}' is not supported using Protostellar")
        };

    public static ProtoKv.MutateInRequest.Types.StoreSemantic ToProto(this CoreKv.StoreSemantics storeSemantics) =>
        storeSemantics switch
        {
            CoreKv.StoreSemantics.Insert => ProtoKv.MutateInRequest.Types.StoreSemantic.Insert,
            CoreKv.StoreSemantics.Replace => ProtoKv.MutateInRequest.Types.StoreSemantic.Replace,
            CoreKv.StoreSemantics.Upsert => ProtoKv.MutateInRequest.Types.StoreSemantic.Upsert,
            _ => System.Enum.TryParse<ProtoKv.MutateInRequest.Types.StoreSemantic>(storeSemantics.ToString(),
                out var stringParsed)
                ? stringParsed
                : throw new ArgumentOutOfRangeException(
                    $"{nameof(CoreKv.StoreSemantics)} '{storeSemantics}' is not supported using Protostellar")
        };

    public static ProtoQuery.QueryRequest.Types.ScanConsistency ToProto(
        this CoreQuery.QueryScanConsistency scanConsistency) =>
        scanConsistency switch
        {
            CoreQuery.QueryScanConsistency.NotBounded => ProtoQuery.QueryRequest.Types.ScanConsistency.NotBounded,
            CoreQuery.QueryScanConsistency.RequestPlus => ProtoQuery.QueryRequest.Types.ScanConsistency.RequestPlus,
            _ => System.Enum.TryParse<ProtoQuery.QueryRequest.Types.ScanConsistency>(scanConsistency.ToString(),
                out var stringParsed)
                ? stringParsed
                : throw new ArgumentOutOfRangeException(
                    $"{nameof(CoreQuery.QueryScanConsistency)} '{scanConsistency}' is not supported using Protostellar")
        };

    public static ProtoLookupInOpCode ToProtoLookupInCode(this CoreOpCode opCode) =>
        opCode switch
        {
            CoreOpCode.Get => ProtoLookupInOpCode.Get, // i.e. GetFull
            CoreOpCode.SubGet => ProtoLookupInOpCode.Get,
            CoreOpCode.SubGetCount => ProtoLookupInOpCode.Count,
            CoreOpCode.SubExist => ProtoLookupInOpCode.Exists,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(opCode),
                message: $"Not a supported LookupIn op code: {opCode}")
        };

    public static ProtoMutateInOpCode ToProtoMutateInCode(this CoreOpCode opCode) =>
        opCode switch
        {
            CoreOpCode.Get => throw new ArgumentOutOfRangeException(nameof(opCode),
                "value is for KV Get, not SubDoc"),
            CoreOpCode.Set => ProtoMutateInOpCode.Replace,
            // taken from https://github.com/couchbase/couchbase-jvm-clients/blob/master/core-io/src/main/java/com/couchbase/client/core/protostellar/kv/CoreProtostellarKeyValueRequests.java
            CoreOpCode.SubCounter => ProtoMutateInOpCode.Counter,
            CoreOpCode.SubReplace => ProtoMutateInOpCode.Replace,
            CoreOpCode.SubDictAdd => ProtoMutateInOpCode.Insert,
            CoreOpCode.SubDictUpsert => ProtoMutateInOpCode.Upsert,
            CoreOpCode.SubArrayPushFirst => ProtoMutateInOpCode.ArrayPrepend,
            CoreOpCode.SubArrayPushLast => ProtoMutateInOpCode.ArrayAppend,
            CoreOpCode.SubArrayAddUnique => ProtoMutateInOpCode.ArrayAddUnique,
            CoreOpCode.SubArrayInsert => ProtoMutateInOpCode.ArrayInsert,
            CoreOpCode.SubDelete => ProtoMutateInOpCode.Remove,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(opCode),
                message: $"Not a supported MutateIn op code: {opCode}")
        };

    public static bool TryConvertScanConsistency(AnalyticsScanConsistency? analyticsScanConsistency,
        out AnalyticsQueryRequest.Types.ScanConsistency protoScanConsistency)
    {
        switch (analyticsScanConsistency)
        {
            case AnalyticsScanConsistency.NotBounded:
                protoScanConsistency = AnalyticsQueryRequest.Types.ScanConsistency.NotBounded;
                return true;
            case AnalyticsScanConsistency.RequestPlus:
                protoScanConsistency =
                    AnalyticsQueryRequest.Types.ScanConsistency.RequestPlus;
                return true;
            default:
                protoScanConsistency = default;
                return false;
        }
    }

    public static AnalyticsQueryRequest.Types.ScanConsistency ToProtoScanConsistency(
        this AnalyticsScanConsistency scanConsistency) =>
        scanConsistency switch
        {
            AnalyticsScanConsistency.NotBounded => AnalyticsQueryRequest.Types.ScanConsistency.NotBounded,
            AnalyticsScanConsistency.RequestPlus => AnalyticsQueryRequest.Types.ScanConsistency.RequestPlus,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(scanConsistency),
                message: $"Not a supported ScanConsistency: {scanConsistency}")
        };

    public static CoreQuery.QueryStatus ToCoreStatus(
        this ProtoQuery.QueryResponse.Types.MetaData.Types.Status queryStatus) => queryStatus switch
    {
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Closed => CoreQuery.QueryStatus.Stopped,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Completed => CoreQuery.QueryStatus.Completed,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Errors => CoreQuery.QueryStatus.Errors,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Running => CoreQuery.QueryStatus.Running,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Stopped => CoreQuery.QueryStatus.Stopped,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Success => CoreQuery.QueryStatus.Success,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Timeout => CoreQuery.QueryStatus.Timeout,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Aborted => CoreQuery.QueryStatus.Fatal,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Fatal => CoreQuery.QueryStatus.Fatal,
        ProtoQuery.QueryResponse.Types.MetaData.Types.Status.Unknown => CoreQuery.QueryStatus.Fatal,
        _ => throw new ArgumentOutOfRangeException(paramName: nameof(queryStatus),
            message: $"Not a supported QueryStatus: {queryStatus}")
    };

    public static SearchQueryRequest.Types.ScanConsistency ToProto(this SearchScanConsistency scanConsistency) =>
        scanConsistency switch
        {
            SearchScanConsistency.NotBounded => SearchQueryRequest.Types.ScanConsistency.NotBounded,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(scanConsistency),
                message: $"Not a supported SearchScanConsistency: {scanConsistency}")
        };

    public static SearchQueryRequest.Types.HighlightStyle ToProto(this HighLightStyle highLightStyle) =>
        highLightStyle switch
        {
            HighLightStyle.Ansi => SearchQueryRequest.Types.HighlightStyle.Ansi,
            HighLightStyle.Html => SearchQueryRequest.Types.HighlightStyle.Html,
            HighLightStyle.None => SearchQueryRequest.Types.HighlightStyle.Default,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(highLightStyle),
                message: $"Not a supported HighLightStyle: {highLightStyle}")
        };

    public static MatchQuery.Types.Operator ToProto(this MatchOperator matchOperator) => matchOperator switch
    {
        MatchOperator.And => MatchQuery.Types.Operator.And,
        MatchOperator.Or => MatchQuery.Types.Operator.Or,
        _ => throw new ArgumentOutOfRangeException(paramName: nameof(matchOperator),
            message: $"Not a supported MatchOperator: {matchOperator}")
    };

    public static ProtoQuery.QueryRequest.Types.ProfileMode ToProto(this Couchbase.Query.QueryProfile coreProfile) =>
        coreProfile switch
        {
            CoreQuery.QueryProfile.Off => ProtoQuery.QueryRequest.Types.ProfileMode.Off,
            CoreQuery.QueryProfile.Phases => ProtoQuery.QueryRequest.Types.ProfileMode.Phases,
            CoreQuery.QueryProfile.Timings => ProtoQuery.QueryRequest.Types.ProfileMode.Timings,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(coreProfile),
                message: $"Not a supported QueryProfile: {coreProfile}")
        };

    public static ProtoFacet ToProto(this ISearchFacet coreFacet)
    {
        switch (coreFacet)
        {
            case DateRangeFacet facet:
            {
                var protoFacet = new Facet
                {
                    DateRangeFacet = new Couchbase.Protostellar.Search.V1.DateRangeFacet
                    {
                        Field = coreFacet.Field,
                        Size = (uint)coreFacet.Size
                    }
                };
                protoFacet.DateRangeFacet.DateRanges.AddRange(
                    facet.DateRanges.Select(range => new DateRange
                    {
                        End = range.End.ToUniversalTime().ToString(),
                        Name = range.Name,
                        Start = range.Start.ToUniversalTime().ToString()
                    }));
                return protoFacet;
            }
            case NumericRangeFacet facet:
            {
                var protoFacet = new Facet
                {
                    NumericRangeFacet = new Couchbase.Protostellar.Search.V1.NumericRangeFacet()
                    {
                        Field = coreFacet.Field,
                        Size = (uint)coreFacet.Size
                    }
                };
                protoFacet.NumericRangeFacet.NumericRanges.AddRange(
                    facet.NumericRanges.Select(range => new NumericRange()
                    {
                        Min = range.Start,
                        Max = range.End,
                        Name = range.Name
                    }));
                return protoFacet;
            }
            case TermFacet facet:
            {
                var protoFacet = new Facet
                {
                    TermFacet = new Couchbase.Protostellar.Search.V1.TermFacet
                    {
                        Field = coreFacet.Field,
                        Size = (uint)coreFacet.Size
                    }
                };
                return protoFacet;
            }
            default:
                throw new ArgumentOutOfRangeException(paramName: nameof(coreFacet),
                    message: $"Not a supported ISearchFacet: {coreFacet}");
        }
    }

    public static Dictionary<string, dynamic> ToCore(this MapField<string, ByteString> protoParams)
    {
        return protoParams.ToDictionary(kvp => kvp.Key, kvp => (dynamic)kvp.Value.ToStringUtf8()); //TODO: Is this the best way to return this? Cast a string into "dynamic"?
    }

    public static Couchbase.Management.Buckets.CompressionMode ToCore(this CompressionMode compressionMode) =>
        compressionMode switch
        {
            CompressionMode.Active => Couchbase.Management.Buckets.CompressionMode.Active,
            CompressionMode.Passive => Couchbase.Management.Buckets.CompressionMode.Passive,
            CompressionMode.Off => Couchbase.Management.Buckets.CompressionMode.Off,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(compressionMode),
                message: $"Not a supported CompressionMode: {compressionMode}")
        };

    public static Couchbase.Management.Buckets.EvictionPolicyType ToCore(this EvictionMode evictionMode) =>
        evictionMode switch
        {
            EvictionMode.Full => EvictionPolicyType.FullEviction,
            EvictionMode.ValueOnly => EvictionPolicyType.ValueOnly,
            EvictionMode.NotRecentlyUsed => EvictionPolicyType.NotRecentlyUsed,
            EvictionMode.None => EvictionPolicyType.NoEviction,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(evictionMode),
                message: $"Not a supported EvictionMode: {evictionMode}")
        };

    public static Couchbase.Management.Buckets.StorageBackend ToCore(this StorageBackend storageBackend) =>
        storageBackend switch
        {
            StorageBackend.Magma => Couchbase.Management.Buckets.StorageBackend.Magma,
            StorageBackend.Couchstore => Couchbase.Management.Buckets.StorageBackend.Couchstore,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(storageBackend),
                message: $"Not a supported StorageBackend: {storageBackend}")
        };

    public static Couchbase.Management.Buckets.ConflictResolutionType ToCore(this ConflictResolutionType conflictResolutionType) =>
        conflictResolutionType switch
        {
            ConflictResolutionType.Custom => Couchbase.Management.Buckets.ConflictResolutionType.Custom,
            ConflictResolutionType.Timestamp => Couchbase.Management.Buckets.ConflictResolutionType.Timestamp,
            ConflictResolutionType.SequenceNumber => Couchbase.Management.Buckets.ConflictResolutionType.SequenceNumber,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(conflictResolutionType),
                message: $"Not a supported ConflictResolutionType: {conflictResolutionType}")
        };
    public static Couchbase.Management.Buckets.BucketType ToCore(this BucketType bucketType) =>
        bucketType switch
        {
            BucketType.Couchbase => Couchbase.Management.Buckets.BucketType.Couchbase,
            BucketType.Ephemeral => Couchbase.Management.Buckets.BucketType.Ephemeral,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(bucketType),
                message: $"Not a supported BucketType: {bucketType}")
        };

    public static Couchbase.KeyValue.DurabilityLevel ToCore(this ProtoKv.DurabilityLevel durabilityLevel) =>
        durabilityLevel switch
        {
            ProtoKv.DurabilityLevel.Majority => CoreKv.DurabilityLevel.Majority,
            ProtoKv.DurabilityLevel.PersistToMajority => CoreKv.DurabilityLevel.PersistToMajority,
            ProtoKv.DurabilityLevel.MajorityAndPersistToActive => CoreKv.DurabilityLevel.MajorityAndPersistToActive,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(durabilityLevel),
                message: $"Not a supported DurabilityLevel: {durabilityLevel}")
        };

    public static ProtoLookupInFlags ToProtoLookupInFlags(this CoreKv.SubdocPathFlags subdocPathFlags) =>
        new ProtoLookupInFlags() { Xattr = subdocPathFlags.HasFlag(CoreKv.SubdocPathFlags.Xattr) };

    public static ProtoMutateInFlags ToProtoMutateInFlags(this CoreKv.SubdocPathFlags subdocPathFlags) =>
        subdocPathFlags.HasFlag(CoreKv.SubdocPathFlags.ExpandMacroValues)
            ? throw new NotSupportedException(nameof(CoreKv.SubdocPathFlags.ExpandMacroValues))
            : new ProtoMutateInFlags()
            {
                Xattr = subdocPathFlags.HasFlag(CoreKv.SubdocPathFlags.Xattr),
                CreatePath = subdocPathFlags.HasFlag(CoreKv.SubdocPathFlags.CreatePath),
            };

    public static Couchbase.KeyValue.IGetResult AsGetResult(this IContentResult contentResult, ITypeSerializer serializer) => new GetResult(
        ExpiryTime: contentResult.Expiry?.ToDateTime(),
        Cas: contentResult.Cas,
        GrpcContentWrapper: new GrpcContentWrapper(contentResult.Content, contentResult.ContentFlags, serializer)
    );

    public static Couchbase.KeyValue.IGetReplicaResult AsGetReplicaResult(this IReplicaContentResult contentResult, ITypeSerializer serializer) => new GetReplicaResult(
        Cas: contentResult.Cas,
        IsActive: contentResult.IsActive,
        GrpcContentWrapper: new GrpcContentWrapper(contentResult.Content, contentResult.ContentFlags, serializer));
}
#endif
