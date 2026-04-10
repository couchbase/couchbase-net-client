#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

#nullable enable

namespace Couchbase.Stellar.Query;

internal class StellarQueryClient : IQueryClient
{
    private readonly StellarCluster _stellarCluster;
    private readonly QueryService.QueryServiceClient _queryClient;
    private readonly ITypeSerializer _typeSerializer;
    private readonly IRetryOrchestrator _retryHandler;

    internal StellarQueryClient(StellarCluster stellarCluster, QueryService.QueryServiceClient queryClient, ITypeSerializer typeSerializer, IRetryOrchestrator  retryHandler)
    {
        _stellarCluster = stellarCluster;
        _queryClient = queryClient;
        _typeSerializer = typeSerializer;
        _retryHandler = retryHandler;
    }

    public DateTime? LastActivity { get; }

    public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options, IRequest? request = null)
    {
        var opts = options?.AsReadOnly() ?? QueryOptions.DefaultReadOnly;

        using var childSpan = _stellarCluster.RequestTracer.RequestSpan(Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.ServiceSpan.N1QLQuery, opts.RequestSpan);
        if (childSpan.CanWrite)
        {
            childSpan.SetAttribute(Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.Attributes.System.Key, Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.Attributes.System.Value);
            childSpan.SetAttribute(Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.Attributes.Service, Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.ServiceSpan.N1QLQuery);
            childSpan.SetAttribute(Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.Attributes.Operation, Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.ServiceSpan.N1QLQuery);
            childSpan.SetAttribute(Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.Attributes.Statement, statement);
            if (opts.BucketName != null) childSpan.SetAttribute(Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.Attributes.BucketName, opts.BucketName);
            if (opts.ScopeName != null) childSpan.SetAttribute(Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.Attributes.ScopeName, opts.ScopeName);
        }

        var protoRequest = new QueryRequest
        {
            Statement = statement,
            ReadOnly = opts.ReadOnly ?? false,
            Prepared = opts.IsPrepared,
            ScanConsistency = opts.ScanConsistency.ToProto(),
            FlexIndex = opts.FlexIndex,
            PreserveExpiry = opts.PreserveExpiry,
        };

        // because of the way the GRPC library handles Optional, we have to use if statements rather than the '??=' operator
        // setting the non-nullable SomeProperty sets the associated HasSomeProperty bool.
        if (opts.BucketName != null) protoRequest.BucketName = opts.BucketName;
        if (opts.ScopeName != null) protoRequest.ScopeName = opts.ScopeName;
        if (opts.CurrentContextId != null) protoRequest.ClientContextId = opts.CurrentContextId;

        var tuningOptions = new QueryRequest.Types.TuningOptions();
        if (opts.MaxServerParallelism.HasValue)
            tuningOptions.MaxParallelism = (uint)opts.MaxServerParallelism.Value;
        if (opts.PipelineBatch.HasValue)
            tuningOptions.PipelineBatch = (uint)opts.PipelineBatch.Value;
        if (opts.PipelineCapacity.HasValue)
            tuningOptions.PipelineCap = (uint)opts.PipelineCapacity.Value;
        if (opts.ScanWait.HasValue)
            tuningOptions.ScanWait = Duration.FromTimeSpan(opts.ScanWait.Value);
        if (opts.ScanCapacity.HasValue) tuningOptions.ScanCap = (uint)opts.ScanCapacity.Value;
        if (opts.IncludeMetrics == false) tuningOptions.DisableMetrics = true;
        if (opts.Parameters.Values.Any()){
            foreach (var (key, value) in opts.Parameters)
            {
                protoRequest.NamedParameters[key] =
                    ByteString.CopyFrom(_typeSerializer.Serialize(value));
            }
        }
        if (opts.Arguments.Any())
        {
            foreach (var arg in opts.Arguments)
            {
                protoRequest.PositionalParameters.Add(
                    ByteString.CopyFrom(_typeSerializer.Serialize(arg)));
            }
        }

        protoRequest.TuningOptions = tuningOptions;
        protoRequest.ProfileMode = opts.Profile.ToProto();

        var stellarRequest = new StellarRequest
        {
            Idempotent = true,
            Token = opts.Token,
            Timeout = opts.TimeOut ?? _stellarCluster.ClusterOptions.QueryTimeout
        };
        stellarRequest.SetMetrics(
            Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.ServiceSpan.N1QLQuery,
            Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.ServiceSpan.N1QLQuery,
            childSpan,
            opts.BucketName,
            opts.ScopeName);

        async Task<IQueryResult<T>> GrpcCall()
        {
            var callOptions = _stellarCluster.GrpcCallOptions(stellarRequest.RemainingTimeout, opts.Token);
            var asyncResponse = _queryClient.Query(protoRequest, callOptions);
            var streamingResult = new StellarQueryResult<T>(asyncResponse, _typeSerializer);
            await streamingResult.InitializeAsync(opts.Token).ConfigureAwait(false);
            return streamingResult;
        }

        return await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }

    public void UpdateClusterCapabilities(ClusterCapabilities clusterCapabilities)
    {
        throw new NotImplementedException();
    }

    public int InvalidateQueryCache()
    {
        throw new NotImplementedException();
    }
}
#endif
