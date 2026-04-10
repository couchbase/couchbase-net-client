#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Retry;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Core.Retry;

namespace Couchbase.Stellar.Analytics;

#nullable enable

internal class StellarAnalyticsClient : AnalyticsService.AnalyticsServiceClient, IAnalyticsClient
{
    private readonly StellarCluster _stellarCluster;
    private readonly AnalyticsService.AnalyticsServiceClient _analyticsClient;
    private readonly IRetryOrchestrator _retryHandler;

    public StellarAnalyticsClient(StellarCluster stellarCluster, AnalyticsService.AnalyticsServiceClient analyticsClient, IRetryOrchestrator retryHandler)
    {
        _stellarCluster = stellarCluster;
        _analyticsClient = analyticsClient;
        _retryHandler = retryHandler;
    }

    public DateTime? LastActivity { get; }

    public async Task<IAnalyticsResult<T>> QueryAsync<T>(string statement, AnalyticsOptions options, IRequest? request = null)
    {
        var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;

        using var childSpan = _stellarCluster.RequestTracer.RequestSpan(OuterRequestSpans.ServiceSpan.AnalyticsQuery, opts.RequestSpan);
        if (childSpan.CanWrite)
        {
            childSpan.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
            childSpan.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.AnalyticsQuery);
            childSpan.SetAttribute(OuterRequestSpans.Attributes.Operation, OuterRequestSpans.ServiceSpan.AnalyticsQuery);
            childSpan.SetAttribute(OuterRequestSpans.Attributes.Statement, statement);
            if (opts.BucketName != null) childSpan.SetAttribute(OuterRequestSpans.Attributes.BucketName, opts.BucketName);
            if (opts.ScopeName != null) childSpan.SetAttribute(OuterRequestSpans.Attributes.ScopeName, opts.ScopeName);
        }

        var analyticsRequest = new AnalyticsQueryRequest
        {
            Statement = statement,
            ReadOnly = opts.Readonly,
            Priority = opts.Priority == -1,
            ScanConsistency = opts.ScanConsistency.ToProtoScanConsistency()
        };
        if (opts.BucketName != null) analyticsRequest.BucketName = opts.BucketName;
        if (opts.ScopeName != null) analyticsRequest.ScopeName = opts.ScopeName;
        if (opts.ClientContextId != null) analyticsRequest.ClientContextId = opts.ClientContextId;

        var stellarRequest = new StellarRequest
        {
            Idempotent = true,
            Token = opts.Token,
            Timeout = opts.Timeout ?? _stellarCluster.ClusterOptions.AnalyticsTimeout
        };
        stellarRequest.SetMetrics(
            OuterRequestSpans.ServiceSpan.AnalyticsQuery,
            OuterRequestSpans.ServiceSpan.AnalyticsQuery,
            childSpan,
            opts.BucketName,
            opts.ScopeName);

        async Task<IAnalyticsResult<T>> GrpcCall()
        {
            var callOptions = _stellarCluster.GrpcCallOptions(stellarRequest.RemainingTimeout, opts.Token);
            var response = _analyticsClient.AnalyticsQuery(analyticsRequest, callOptions);
            return new ProtoAnalyticsResult<T>(response, _stellarCluster.TypeSerializer);
        }

        return await _retryHandler.RetryAsync(GrpcCall, stellarRequest).ConfigureAwait(false);
    }
}
#endif
