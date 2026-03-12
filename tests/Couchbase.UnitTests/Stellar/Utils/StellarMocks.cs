#nullable enable
#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Query;
using Couchbase.Stellar;
using Couchbase.Stellar.Search;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;

namespace Couchbase.UnitTests.Stellar.Utils;

internal static class StellarMocks
{
    public static StellarCluster CreateClusterFromMocks(IOperationCompressor? compressor = null)
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

        // Configure the retry orchestrator to return mock responses for KV operations.
        // This allows tests to complete successfully without hitting the network.
        var mockMutationToken = new MutationToken { BucketName = "test", VbucketId = 1, VbucketUuid = 1, SeqNo = 1 };
        requestOrchestrator
            .Setup(x => x.RetryAsync(It.IsAny<Func<Task<InsertResponse>>>(), It.IsAny<IRequest>()))
            .ReturnsAsync(new InsertResponse { Cas = 1, MutationToken = mockMutationToken });
        requestOrchestrator
            .Setup(x => x.RetryAsync(It.IsAny<Func<Task<UpsertResponse>>>(), It.IsAny<IRequest>()))
            .ReturnsAsync(new UpsertResponse { Cas = 1, MutationToken = mockMutationToken });
        requestOrchestrator
            .Setup(x => x.RetryAsync(It.IsAny<Func<Task<ReplaceResponse>>>(), It.IsAny<IRequest>()))
            .ReturnsAsync(new ReplaceResponse { Cas = 1, MutationToken = mockMutationToken });

        // If a compressor was explicitly provided, indicate that a real compression algorithm (Snappy) is available.
        // Otherwise, use a default mock compressor with CompressionAlgorithm.None (compression disabled).
        var effectiveCompressor = compressor ?? new Mock<IOperationCompressor>().Object;
        var algorithm = compressor != null ? CompressionAlgorithm.Snappy : CompressionAlgorithm.None;

        return new StellarCluster(bucketManager.Object, searchIndexManager.Object, queryIndexManager.Object,
            queryServiceClient.Object, analyticsClient.Object, searchClient.Object, metaData,
            requestTracer.Object, channel, typeSerializer.Object, requestOrchestrator.Object, clusterOptions, effectiveCompressor, algorithm);
    }

    public static StellarCluster CreateClusterFromMocksWithOptions(
        IOperationCompressor? compressor = null,
        Action<ClusterOptions>? configureOptions = null)
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
        configureOptions?.Invoke(clusterOptions);
        var requestOrchestrator = new Mock<IRetryOrchestrator>();

        var effectiveCompressor = compressor ?? new Mock<IOperationCompressor>().Object;
        var algorithm = compressor != null ? CompressionAlgorithm.Snappy : CompressionAlgorithm.None;

        return new StellarCluster(bucketManager.Object, searchIndexManager.Object, queryIndexManager.Object,
            queryServiceClient.Object, analyticsClient.Object, searchClient.Object, metaData,
            requestTracer.Object, channel, typeSerializer.Object, requestOrchestrator.Object, clusterOptions, effectiveCompressor, algorithm);
    }
}
#endif
