#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Stellar.Core;

namespace Couchbase.Stellar.Analytics;

#nullable enable

internal class StellarAnalyticsClient : AnalyticsService.AnalyticsServiceClient, IAnalyticsClient
{
    private readonly StellarCluster _stellarCluster;
    private readonly AnalyticsService.AnalyticsServiceClient _analyticsClient;

    public StellarAnalyticsClient(StellarCluster stellarCluster)
    {
        _stellarCluster = stellarCluster;
        _analyticsClient = new AnalyticsService.AnalyticsServiceClient(_stellarCluster.GrpcChannel);
    }

    public DateTime? LastActivity { get; }

    public Task<IAnalyticsResult<T>> QueryAsync<T>(string statement, AnalyticsOptions options)
    {
        var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;
        var request = new AnalyticsQueryRequest
        {
            Statement = statement,
            ReadOnly = opts.Readonly,
            Priority = opts.Priority == -1,
            ScanConsistency = opts.ScanConsistency.ToProtoScanConsistency()
        };
        if (opts.BucketName != null) request.BucketName = opts.BucketName;
        if (opts.ScopeName != null) request.ScopeName = opts.ScopeName;
        if (opts.ClientContextId != null) request.ReadOnly = opts.Readonly;

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = _analyticsClient.AnalyticsQuery(request, callOptions);

        return Task.FromResult<IAnalyticsResult<T>>(new ProtoAnalyticsResult<T>(response, _stellarCluster.TypeSerializer));
    }
}
#endif
