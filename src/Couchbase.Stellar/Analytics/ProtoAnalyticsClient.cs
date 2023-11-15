using Couchbase.Analytics;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Stellar.Core;

namespace Couchbase.Stellar.Analytics;

internal class ProtoAnalyticsClient : AnalyticsService.AnalyticsServiceClient
{
    private readonly StellarCluster _stellarCluster;
    private readonly AnalyticsService.AnalyticsServiceClient _analyticsClient;

    public ProtoAnalyticsClient(StellarCluster stellarCluster)
    {
        _stellarCluster = stellarCluster;
        _analyticsClient = new AnalyticsService.AnalyticsServiceClient(_stellarCluster.GrpcChannel);
    }

    public Task<IAnalyticsResult<T>> QueryAsync<T>(AnalyticsQueryRequest analyticsRequest, AnalyticsOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;

        analyticsRequest.ScanConsistency = opts.ScanConsistency.ToProtoScanConsistency();

        var callOptions = _stellarCluster.GrpcCallOptions(opts.Timeout, opts.Token);
        var response = _analyticsClient.AnalyticsQuery(analyticsRequest, callOptions);

        return Task.FromResult<IAnalyticsResult<T>>(new ProtoAnalyticsResult<T>(response, _stellarCluster.TypeSerializer));
    }
}
