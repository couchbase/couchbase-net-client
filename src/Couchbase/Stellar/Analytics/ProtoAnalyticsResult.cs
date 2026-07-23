#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Stellar.Core.Retry;
using Grpc.Core;

namespace Couchbase.Stellar.Analytics;

#nullable enable

public class ProtoAnalyticsResult<T> : IAnalyticsResult<T>
{
    private readonly AsyncServerStreamingCall<AnalyticsQueryResponse> _streamingAnalyticsResponse;
    private readonly IAsyncStreamReader<AnalyticsQueryResponse> _responseStream;
    private readonly ITypeSerializer _serializer;
    private readonly Func<Exception, Exception> _mapMidStreamError;
    private readonly List<T> _tempResults = new();
    private bool _hasReadHeader;
    private bool _firstResponseHadRows;

    public ProtoAnalyticsResult(AsyncServerStreamingCall<AnalyticsQueryResponse> streamingAnalyticsResponse, ITypeSerializer serializer, Func<Exception, Exception> mapMidStreamError)
    {
        _streamingAnalyticsResponse = streamingAnalyticsResponse;
        _responseStream = _streamingAnalyticsResponse.ResponseStream;
        _serializer = serializer;
        _mapMidStreamError = mapMidStreamError;
    }

    public void Dispose()
    {
        _streamingAnalyticsResponse.Dispose();
    }

    /// <summary>
    /// Reads the first streamed response. This runs under the retry orchestrator (called from
    /// StellarAnalyticsClient.GrpcCall inside RetryAsync), so a retryable failure on the first
    /// response propagates and is retried per RFC 77 — no rows have been delivered yet. There is
    /// deliberately no try/catch here: only genuinely mid-stream failures (in the enumerator, after
    /// the first response) are mapped to RequestCanceled via <see cref="_mapMidStreamError"/>.
    /// </summary>
    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_hasReadHeader)
        {
            return;
        }
        _hasReadHeader = true;

        _firstResponseHadRows = await _responseStream.MoveNext(cancellationToken).ConfigureAwait(false);
        if (_firstResponseHadRows)
        {
            SetMetaData(_responseStream.Current);
            foreach (var protoRow in _responseStream.Current.Rows)
            {
                if (cancellationToken.IsCancellationRequested || protoRow == null)
                {
                    break;
                }
                if (protoRow.IsEmpty)
                {
                    continue;
                }
                _tempResults.Add(_serializer.Deserialize<T>(protoRow.Memory)!);
            }
        }
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    {
        // Normally already read inside the retry orchestrator; idempotent, so this is a safety net
        // for callers that enumerate a result constructed outside GrpcCall.
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        // Rows buffered from the first response (read under the retry orchestrator).
        foreach (var row in _tempResults)
        {
            yield return row;
        }
        _tempResults.Clear();

        // If the first read reported end-of-stream there is nothing more to enumerate.
        if (!_firstResponseHadRows)
        {
            yield break;
        }

        // Subsequent responses are genuinely mid-stream: a failure here cannot be retried (rows may
        // already have been delivered), so map it to the exception to throw (see MapMidStreamException).
        // MoveNext sits in the try because a yield can't live inside a catch.
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await _responseStream.MoveNext(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is RpcException || StellarRetryHandler.IsTransientTransportException(e))
            {
                throw _mapMidStreamError(e);
            }

            if (!hasNext) break;

            SetMetaData(_responseStream.Current);

            foreach (var protoRow in _responseStream.Current.Rows)
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

    private void SetMetaData(AnalyticsQueryResponse response)
    {
        var responseMetaData = response.MetaData;
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
    }

    public RetryReason RetryReason { get; }
    public IAsyncEnumerable<T> Rows => this;
    public AnalyticsMetaData? MetaData { get; private set;}
}
#endif
