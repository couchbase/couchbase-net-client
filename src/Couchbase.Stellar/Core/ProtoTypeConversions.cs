// Types and methods for easing conversion between equivalent types in Couchbase.Core.* and GRPC-generated types.

using Couchbase.Analytics;
using Couchbase.Core.IO.Serializers;
using Couchbase.Protostellar.Admin.Query.V1;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Protostellar.Search.V1;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.Stellar.KeyValue;
using CoreKv = Couchbase.KeyValue;
using CoreQuery = Couchbase.Query;
using CoreOpCode = Couchbase.Core.IO.Operations.OpCode;
using MatchQuery = Couchbase.Protostellar.Search.V1.MatchQuery;
using ProtoKv = Couchbase.Protostellar.KV.V1;
using ProtoQuery = Couchbase.Protostellar.Query.V1;
using ProtoLookupInOpCode = Couchbase.Protostellar.KV.V1.LookupInRequest.Types.Spec.Types.Operation;
using ProtoLookupInFlags = Couchbase.Protostellar.KV.V1.LookupInRequest.Types.Spec.Types.Flags;
using ProtoMutateInOpCode = Couchbase.Protostellar.KV.V1.MutateInRequest.Types.Spec.Types.Operation;
using ProtoMutateInFlags = Couchbase.Protostellar.KV.V1.MutateInRequest.Types.Spec.Types.Flags;

namespace Couchbase.Stellar.Core;

internal static class TypeConversionExtensions
{
    public static ProtoKv.DurabilityLevel? ToProto(this CoreKv.DurabilityLevel durabilityLevel) =>
        durabilityLevel switch
        {
            CoreKv.DurabilityLevel.None => null,
            CoreKv.DurabilityLevel.Majority => ProtoKv.DurabilityLevel.Majority,
            CoreKv.DurabilityLevel.PersistToMajority => ProtoKv.DurabilityLevel.PersistToMajority,
            CoreKv.DurabilityLevel.MajorityAndPersistToActive => ProtoKv.DurabilityLevel.MajorityAndPersistToActive,
            _ => throw new ArgumentOutOfRangeException($"{nameof(CoreKv.DurabilityLevel)} '{durabilityLevel}' is not supported using Protostellar")
        };

    public static ProtoKv.MutateInRequest.Types.StoreSemantic ToProto(this CoreKv.StoreSemantics storeSemantics) =>
        storeSemantics switch
        {
            CoreKv.StoreSemantics.Insert => ProtoKv.MutateInRequest.Types.StoreSemantic.Insert,
            CoreKv.StoreSemantics.Replace => ProtoKv.MutateInRequest.Types.StoreSemantic.Replace,
            CoreKv.StoreSemantics.Upsert => ProtoKv.MutateInRequest.Types.StoreSemantic.Upsert,
            _ => Enum.TryParse<ProtoKv.MutateInRequest.Types.StoreSemantic>(storeSemantics.ToString(), out var stringParsed)
                ? stringParsed
                : throw new ArgumentOutOfRangeException($"{nameof(CoreKv.StoreSemantics)} '{storeSemantics}' is not supported using Protostellar")
        };

    public static ProtoQuery.QueryRequest.Types.ScanConsistency ToProto(
        this CoreQuery.QueryScanConsistency scanConsistency) =>
        scanConsistency switch
        {
            CoreQuery.QueryScanConsistency.NotBounded => ProtoQuery.QueryRequest.Types.ScanConsistency.NotBounded,
            CoreQuery.QueryScanConsistency.RequestPlus => ProtoQuery.QueryRequest.Types.ScanConsistency.RequestPlus,
            _ => Enum.TryParse<ProtoQuery.QueryRequest.Types.ScanConsistency>(scanConsistency.ToString(), out var stringParsed)
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
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(opCode), message: $"Not a valid LookupIn op code: {opCode}" )
        };

    public static ProtoMutateInOpCode ToProtoMutateInCode(this CoreOpCode opCode) =>
        opCode switch
        {
            CoreOpCode.Get => throw new ArgumentOutOfRangeException(nameof(opCode),
                "value is for KV Get, not SubDoc"),

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
                message: $"Not a valid MutateIn op code: {opCode}")
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

    public static AnalyticsQueryRequest.Types.ScanConsistency ToProtoScanConsistency(this AnalyticsScanConsistency scanConsistency) =>
        scanConsistency switch
        {
            AnalyticsScanConsistency.NotBounded => AnalyticsQueryRequest.Types.ScanConsistency.NotBounded,
            AnalyticsScanConsistency.RequestPlus => AnalyticsQueryRequest.Types.ScanConsistency.RequestPlus,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(scanConsistency), message: $"Not a valid ScanConsistency: {scanConsistency}" )
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
            message: $"Not a valid QueryStatus: {queryStatus}")
    };

    public static SearchQueryRequest.Types.ScanConsistency ToProto(this SearchScanConsistency scanConsistency) =>
        scanConsistency switch
        {
            SearchScanConsistency.NotBounded => SearchQueryRequest.Types.ScanConsistency.NotBounded,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(scanConsistency),
                message: $"Not a valid SearchScanConsistency: {scanConsistency}")
        };

    public static SearchQueryRequest.Types.HighlightStyle ToProto(this HighLightStyle highLightStyle) =>
        highLightStyle switch
        {
            HighLightStyle.Ansi => SearchQueryRequest.Types.HighlightStyle.Ansi,
            HighLightStyle.Html => SearchQueryRequest.Types.HighlightStyle.Html,
            HighLightStyle.None => SearchQueryRequest.Types.HighlightStyle.Default,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(highLightStyle),
                message: $"Not a valid HighLightStyle: {highLightStyle}")
        };

    public static MatchQuery.Types.Operator ToProto(this MatchOperator matchOperator) => matchOperator switch
    {
        MatchOperator.And => MatchQuery.Types.Operator.And,
        MatchOperator.Or => MatchQuery.Types.Operator.Or,
        _ => throw new ArgumentOutOfRangeException(paramName: nameof(matchOperator), message: $"Not a valid MatchOperator: {matchOperator}")
    };

    //TODO: Might not be needed
    public static Couchbase.Management.Views.IndexType ToCore(this IndexType indexType) => indexType switch
    {
        IndexType.Gsi => Couchbase.Management.Views.IndexType.Gsi,
        IndexType.View => Couchbase.Management.Views.IndexType.View,
        _ => throw new ArgumentOutOfRangeException()
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
}
