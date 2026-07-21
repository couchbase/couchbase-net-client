#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;

namespace Couchbase.Stellar.Query;

#nullable enable

internal class StellarQueryResult<T> : IQueryResult<T>
{
    private readonly AsyncServerStreamingCall<QueryResponse> _streamingQueryResponse;
    private readonly ITypeSerializer _serializer;
    private readonly IAsyncStreamReader<QueryResponse> _streamReader;
    private readonly Action<Exception> _onStreamError;
    private readonly List<T> _tempResults = new();
    private bool _hasReadHeader;
    private volatile bool _disposed;

    public StellarQueryResult(AsyncServerStreamingCall<QueryResponse> streamingQueryResponse, ITypeSerializer serializer, Action<Exception> onStreamError)
    {
        _streamingQueryResponse = streamingQueryResponse;
        _streamReader = _streamingQueryResponse.ResponseStream;
        _serializer = serializer;
        _onStreamError = onStreamError;
    }
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _streamingQueryResponse.Dispose();
        }
    }

    private bool ProfileIsNull(ByteString profile)
    {
        return  profile.IsEmpty || profile.ToStringUtf8() == "null";
    }

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!_hasReadHeader)
        {
            _hasReadHeader = true;
            await _streamReader.MoveNext(cancellationToken).ConfigureAwait(false);
            if (_streamReader.Current.MetaData != null)
            {
                var responseMetaData = _streamReader.Current.MetaData;
                if (responseMetaData.HasProfile && !ProfileIsNull(responseMetaData.Profile)) MetaData!.Profile = responseMetaData.Profile.ToStringUtf8();
                if (responseMetaData.Metrics != null) MetaData!.Metrics = ConvertQueryMetrics(responseMetaData.Metrics);
                if (responseMetaData.Signature != null) MetaData!.Signature = responseMetaData.Signature.ToStringUtf8();
                if (responseMetaData.Warnings != null)
                    MetaData!.Warnings = ConvertQueryWarnings(responseMetaData.Warnings);
                if (responseMetaData.RequestId != null) MetaData!.RequestId = responseMetaData.RequestId;
                if (responseMetaData.ClientContextId != null)
                    MetaData!.ClientContextId = responseMetaData.ClientContextId;
                MetaData!.Status = responseMetaData.Status.ToCoreStatus();
            }

            foreach (var queryResponse in _streamReader.Current.Rows)
            {
                if (queryResponse == null) continue;
                var deserializedObj = _serializer.Deserialize<T>(queryResponse.Memory);
                if (deserializedObj == null)
                {
                    Errors.Add(new Error { Message = "Failed to deserialize item." });
                }
                else
                {
                    _tempResults.Add(deserializedObj);
                }
            }
        }
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
    {
        if (_hasReadHeader)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

        //enumerate the first response
        foreach (var result in _tempResults)
        {
            yield return result;
        }
        _tempResults.Clear();

        //enumerate the rest of the responses. The first response was already read under the retry
        //orchestrator (see StellarQueryClient); anything that fails here is mid-stream and cannot be
        //retried. Per RFC 77 a retryable error is surfaced as RequestCancelled and a terminal error is
        //mapped normally, both via _onStreamError. (MoveNext is read inside the try; the yields stay
        //outside it because C# forbids `yield return` inside a try that has a catch.)
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = await _streamReader.MoveNext(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is RpcException || StellarRetryHandler.IsTransientTransportException(e))
            {
                _onStreamError(e); // always throws
                throw; // unreachable, satisfies definite assignment
            }

            if (!hasNext) break;

            foreach (var queryResponse in _streamReader.Current.Rows)
            {
                if (queryResponse == null) continue;
                var deserializedObj = _serializer.Deserialize<T>(queryResponse.Memory);
                if (deserializedObj == null)
                {
                    Errors.Add(new Error { Message = "Failed to deserialize item." });
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
        if (protoMetrics.ElapsedTime != null)
            coreMetrics.ElapsedTime = protoMetrics.ElapsedTime.ToString().Trim('"'); //TODO: Is this the expected string format?
        if (protoMetrics.ExecutionTime != null)
            coreMetrics.ExecutionTime = protoMetrics.ExecutionTime.ToString().Trim('"');
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
#endif
