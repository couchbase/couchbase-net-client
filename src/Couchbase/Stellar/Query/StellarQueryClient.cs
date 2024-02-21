#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;
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

    public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options)
    {
        var opts = options?.AsReadOnly() ?? QueryOptions.DefaultReadOnly;

        var request = new QueryRequest
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
        if (opts.BucketName != null) request.BucketName = opts.BucketName;
        if (opts.ScopeName != null) request.ScopeName = opts.ScopeName;
        if (opts.CurrentContextId != null) request.ClientContextId = opts.CurrentContextId;

        var tuningOptions = new QueryRequest.Types.TuningOptions();
        if (opts.MaxServerParallelism.HasValue) tuningOptions.MaxParallelism = (uint)opts.MaxServerParallelism.Value;
        if (opts.PipelineBatch.HasValue) tuningOptions.PipelineBatch = (uint)opts.PipelineBatch.Value;
        if (opts.PipelineCapacity.HasValue) tuningOptions.PipelineCap = (uint)opts.PipelineCapacity.Value;
        if (opts.ScanWait.HasValue) tuningOptions.ScanWait = Duration.FromTimeSpan(opts.ScanWait.Value);
        if (opts.ScanCapacity.HasValue) tuningOptions.ScanCap = (uint)opts.ScanCapacity.Value;
        if (opts.IncludeMetrics == false) tuningOptions.DisableMetrics = true;
        request.TuningOptions = tuningOptions;
        request.ProfileMode = opts.Profile.ToProto();

        async Task<IQueryResult<T>> GrpcCall()
        {
            var callOptions = _stellarCluster.GrpcCallOptions(opts!.TimeOut, opts.Token);
            var asyncResponse = _queryClient.Query(request, callOptions);
            var streamingResult = new StellarQueryResult<T>(asyncResponse, _typeSerializer);
            await streamingResult.InitializeAsync(opts.Token).ConfigureAwait(false);
            return streamingResult;
        }

        var stellarRequest = new StellarRequest
        {
            Idempotent = true,
            Token = opts.Token
        };

        return _retryHandler.RetryAsync(GrpcCall, stellarRequest);
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
