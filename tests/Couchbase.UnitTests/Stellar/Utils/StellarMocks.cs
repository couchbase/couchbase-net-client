#if NETCOREAPP3_1_OR_GREATER
using System;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar;
using Couchbase.Stellar.Core;
using Couchbase.Stellar.Search;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;

namespace Couchbase.UnitTests.Stellar.Utils;

internal static class StellarMocks
{
    public static StellarCluster CreateClusterFromMocks()
    {
        var channel = GrpcChannel.ForAddress(new Uri("https://xxx"));
        var bucketManager = new Mock<IBucketManager>();
        var searchIndexManager = new Mock<ISearchIndexManager>();
        var queryServiceClient = new Mock<IQueryClient>();
        var analyticsClient = new Mock<IAnalyticsClient>();
        var searchClient = new Mock<IStellarSearchClient>();
        var queryIndexManager = new Mock<IQueryIndexManager>();
        var metaData = new Metadata();
        var requestTracer = new Mock<IRequestTracer>();
        var typeSerializer = new Mock<ITypeSerializer>();
        var clusterOptions = new ClusterOptions();
        var requestOrchestrator = new Mock<IRetryOrchestrator>();

        return new StellarCluster(bucketManager.Object, searchIndexManager.Object, queryIndexManager.Object,
            queryServiceClient.Object, analyticsClient.Object, searchClient.Object, metaData,
            requestTracer.Object, channel, typeSerializer.Object, requestOrchestrator.Object, clusterOptions);
    }
}
#endif
