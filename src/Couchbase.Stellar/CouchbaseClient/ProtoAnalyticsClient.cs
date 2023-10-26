using Couchbase.Analytics;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Stellar.Util;

namespace Couchbase.Stellar.CouchbaseClient
{
    internal class ProtoAnalyticsClient : AnalyticsService.AnalyticsServiceClient
    {
        private readonly ProtoCluster _protoCluster;
        private readonly AnalyticsService.AnalyticsServiceClient _analyticsClient;

        public ProtoAnalyticsClient(ProtoCluster protoCluster)
        {
            _protoCluster = protoCluster;
            _analyticsClient = new AnalyticsService.AnalyticsServiceClient(_protoCluster.GrpcChannel);
        }

        public Task<IAnalyticsResult<T>> QueryAsync<T>(AnalyticsQueryRequest analyticsRequest, AnalyticsOptions? options = null)
        {
            var opts = options?.AsReadOnly() ?? AnalyticsOptions.DefaultReadOnly;
            var serializer = _protoCluster.TypeSerializer;

            analyticsRequest.ScanConsistency = opts.ScanConsistency.ToProtoScanConsistency();

            var callOptions = _protoCluster.GrpcCallOptions(opts.Timeout, opts.Token);
            var response = _analyticsClient.AnalyticsQuery(analyticsRequest, callOptions);

            return Task.FromResult<IAnalyticsResult<T>>(new ProtoAnalyticsResult<T>(response, _protoCluster.TypeSerializer));
        }
    }
}
