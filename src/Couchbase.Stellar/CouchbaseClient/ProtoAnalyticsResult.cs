using Couchbase.Analytics;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Analytics.V1;
using Grpc.Core;

namespace Couchbase.Stellar.CouchbaseClient;

public class ProtoAnalyticsResult<T> : IAnalyticsResult<T>
{
    private readonly AsyncServerStreamingCall<AnalyticsQueryResponse> _streamingAnalyticsResponse;
    private readonly ITypeSerializer _serializer;

    public ProtoAnalyticsResult(AsyncServerStreamingCall<AnalyticsQueryResponse> streamingAnalyticsResponse, ITypeSerializer serializer)
    {
        _streamingAnalyticsResponse = streamingAnalyticsResponse;
        _serializer = serializer;
    }

    public void Dispose()
    {
        _streamingAnalyticsResponse.Dispose();
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        var responseStream = _streamingAnalyticsResponse.ResponseStream;
        while (await responseStream.MoveNext(cancellationToken).ConfigureAwait(false))
        {
            var responseMetaData = responseStream.Current.MetaData;
            MetaData = new AnalyticsMetaData
            {
                ClientContextId = responseMetaData.ClientContextId,
                RequestId = responseMetaData.RequestId,
                Signature = responseMetaData.Signature,
                Status = AnalyticsStatus.Success
            };

            MetaData.Metrics = new AnalyticsMetrics
            {
                ElaspedTime = responseMetaData.Metrics.ElapsedTime.ToTimeSpan().ToString(),
                ExecutionTime = responseMetaData.Metrics.ExecutionTime.ToTimeSpan().ToString(),
                ResultCount = (uint)responseMetaData.Metrics.ResultCount,
                ResultSize = (uint)responseMetaData.Metrics.ResultSize,
                MutationCount = (uint)responseMetaData.Metrics.MutationCount,
                ErrorCount = (uint)responseMetaData.Metrics.ErrorCount,
                WarningCount = (uint)responseMetaData.Metrics.WarningCount,
                SortCount = (uint)responseMetaData.Metrics.SortCount
            };

            MetaData.Warnings = new List<AnalyticsWarning>(responseMetaData.Warnings.Count);
            MetaData.Warnings.AddRange(responseMetaData.Warnings.Select(warning => new AnalyticsWarning { Code = (int)warning.Code, Message = warning.Message }));

            foreach (var protoRow in responseStream.Current.Rows)
            {
                if (cancellationToken.IsCancellationRequested || protoRow == null)
                {
                    break;
                }

                if (protoRow.IsEmpty)
                {
                    continue;
                }
                var analyticsRow = _serializer.Deserialize<T>(protoRow.Memory);

                yield return analyticsRow!;
            }
        }
    }

    public RetryReason RetryReason { get; }
    public IAsyncEnumerable<T> Rows => this;
    public AnalyticsMetaData? MetaData { get; private set;}
}
