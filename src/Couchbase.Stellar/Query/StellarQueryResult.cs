using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core;
using Google.Protobuf.Collections;
using Grpc.Core;

namespace Couchbase.Stellar.Query;

internal class StellarQueryResult<T> : IQueryResult<T>
{
    private readonly AsyncServerStreamingCall<QueryResponse> _streamingQueryResponse;
    private readonly ITypeSerializer _serializer;

    public StellarQueryResult(AsyncServerStreamingCall<QueryResponse> streamingQueryResponse, ITypeSerializer serializer)
    {
        _streamingQueryResponse = streamingQueryResponse;
        _serializer = serializer;
    }
    public void Dispose()
    {
        _streamingQueryResponse.Dispose();
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
    {
        var responseStream = _streamingQueryResponse.ResponseStream;
        while (await responseStream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            if (responseStream.Current.MetaData != null)
            {
                var responseMetaData = responseStream.Current.MetaData;
                if (responseMetaData.HasProfile) MetaData!.Profile = responseMetaData.Profile;
                if (responseMetaData.Metrics != null) MetaData!.Metrics = ConvertQueryMetrics(responseMetaData.Metrics);
                if (responseMetaData.Profile != null) MetaData!.Profile = responseMetaData.Profile.ToStringUtf8();
                if (responseMetaData.Signature != null) MetaData!.Signature = responseMetaData.Signature.ToStringUtf8();
                if (responseMetaData.Warnings != null) MetaData!.Warnings = ConvertQueryWarnings(responseMetaData.Warnings);
                if (responseMetaData.RequestId != null) MetaData!.RequestId = responseMetaData.RequestId;
                if (responseMetaData.ClientContextId != null) MetaData!.ClientContextId = responseMetaData.ClientContextId;
                MetaData!.Status = responseMetaData.Status.ToCoreStatus();
            }

            foreach (var queryResponse in responseStream.Current.Rows)
            {
                if (queryResponse == null) continue;
                var deserializedObj = _serializer.Deserialize<T>(queryResponse.Memory);
                if (deserializedObj == null)
                {
                    Errors.Add(new Error { Message = "Failed to deserialize item."});
                }
                else
                {
                    yield return deserializedObj;
                }
            }
        }
    }

    private QueryMetrics ConvertQueryMetrics(QueryResponse.Types.MetaData.Types.Metrics protoMetrics)
    {
        var coreMetrics = new QueryMetrics
        {
            ErrorCount = (uint)protoMetrics.ErrorCount,
            MutationCount = (uint)protoMetrics.MutationCount,
            ResultCount = (uint)protoMetrics.ResultCount,
            SortCount = (uint)protoMetrics.SortCount,
            WarningCount = (uint)protoMetrics.WarningCount
        };
        if (protoMetrics.ElapsedTime != null) coreMetrics.ElapsedTime = protoMetrics.ElapsedTime.ToTimeSpan().ToString(); //TODO: Is this the expected string format?
        if (protoMetrics.ExecutionTime != null) coreMetrics.ExecutionTime = protoMetrics.ExecutionTime.ToTimeSpan().ToString();
        return coreMetrics;
    }

    private List<QueryWarning> ConvertQueryWarnings(RepeatedField<QueryResponse.Types.MetaData.Types.Warning> protoWarnings)
    {
        var coreWarnings = new List<QueryWarning>(protoWarnings.Count);
        coreWarnings.AddRange(protoWarnings.Select(warning => new QueryWarning { Message = warning.Message, Code = (int)warning.Code }));
        return coreWarnings;
    }

    public RetryReason RetryReason { get; } = RetryReason.NoRetry; // FIXME
    public IAsyncEnumerable<T> Rows => this;
    public QueryMetaData? MetaData { get; init; } = new QueryMetaData();
    public List<Error> Errors { get; } = new();
}
