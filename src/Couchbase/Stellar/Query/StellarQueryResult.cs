#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
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
    private readonly Func<Exception, Exception> _mapMidStreamError;
    private readonly List<T> _tempResults = new();
    private bool _hasReadHeader;
    private volatile bool _disposed;
    private IRequestSpan? _span;

    public StellarQueryResult(AsyncServerStreamingCall<QueryResponse> streamingQueryResponse, ITypeSerializer serializer, Func<Exception, Exception> mapMidStreamError)
    {
        _streamingQueryResponse = streamingQueryResponse;
        _streamReader = _streamingQueryResponse.ResponseStream;
        _serializer = serializer;
        _mapMidStreamError = mapMidStreamError;
    }
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _streamingQueryResponse.Dispose();
        }
        // After the call is disposed, so that the gRPC span ends first.
        EndSpan();
    }

    /// <summary>
    /// Ends <paramref name="span"/> when the response stream closes: when enumeration reads to the
    /// end of the stream, when the stream fails, or when this result is disposed (RFC 77).
    /// </summary>
    internal void EndSpanWhenStreamCloses(IRequestSpan span)
    {
        _span = span;
        if (_disposed)
        {
            EndSpan();
        }
    }

    private void EndSpan() => Interlocked.Exchange(ref _span, null)?.End();

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
        // Idempotent (guarded by _hasReadHeader): normally the first response was already read inside
        // the retry orchestrator via GrpcCall, but call it unconditionally so direct enumeration still
        // reads the header/first response rather than treating it as mid-stream.
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        //enumerate the first response
        foreach (var result in _tempResults)
        {
            yield return result;
        }
        _tempResults.Clear();

        // First response was already read under the retry orchestrator; a failure here is
        // mid-stream. Map it to the exception to throw (see MapMidStreamException). MoveNext sits in
        // the try because a yield can't live inside a catch.
        // The span ends here only if the stream closed. If the caller stops early, the stream
        // stays open and Dispose ends the span.
        var streamClosed = false;
        try
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await _streamReader.MoveNext(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    streamClosed = true;
                    if (e is RpcException || StellarRetryHandler.IsTransientTransportException(e))
                    {
                        throw _mapMidStreamError(e);
                    }
                    throw;
                }

                if (!hasNext)
                {
                    streamClosed = true;
                    break;
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
                        yield return deserializedObj;
                    }
                }
            }
        }
        finally
        {
            if (streamClosed)
            {
                EndSpan();
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
