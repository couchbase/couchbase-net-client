using System;
using Couchbase.Core.Exceptions;
using Couchbase.Grpc.Protocol.Sdk.Search;
using Couchbase.Search;
using SearchScanConsistency = Couchbase.Search.SearchScanConsistency;

namespace Couchbase.FitPerformer.Utils.Options;

internal static class ProtoTypeExtensions
{
    public static Couchbase.Search.SearchScanConsistency ToCore(this Couchbase.Grpc.Protocol.Sdk.Search.SearchScanConsistency scanConsistency) =>
        scanConsistency switch
        {
            Grpc.Protocol.Sdk.Search.SearchScanConsistency.NotBounded => SearchScanConsistency.NotBounded,
            _ => throw new ArgumentOutOfRangeException($"{nameof(Grpc.Protocol.Sdk.Search.SearchScanConsistency)} '{scanConsistency}' could notn be parsed to Core type.")
        };

    public static Couchbase.Search.HighLightStyle ToCore(this HighlightStyle highLightStyle) =>
        highLightStyle switch
        {
            Couchbase.Grpc.Protocol.Sdk.Search.HighlightStyle.Ansi => Couchbase.Search.HighLightStyle.Ansi,
            Couchbase.Grpc.Protocol.Sdk.Search.HighlightStyle.Html => Couchbase.Search.HighLightStyle.Html,
            _ => throw new ArgumentOutOfRangeException(paramName: nameof(highLightStyle),
                message: $"Could not parse HighLightStyle to Core: {highLightStyle}")
        };

    public static Couchbase.Search.Queries.Simple.MatchOperator ToCore(
        this Couchbase.Grpc.Protocol.Sdk.Search.MatchOperator protoMatchOperator) => protoMatchOperator switch
    {
        MatchOperator.SearchMatchOperatorAnd => Search.Queries.Simple.MatchOperator.And,
        MatchOperator.SearchMatchOperatorOr => Search.Queries.Simple.MatchOperator.Or,
        _ => throw new ArgumentOutOfRangeException($"Could not parse MatchOperator to Core: {protoMatchOperator}")
    };
}