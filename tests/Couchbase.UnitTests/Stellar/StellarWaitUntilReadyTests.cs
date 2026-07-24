#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Serializers;
using Couchbase.Diagnostics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Query;
using Couchbase.Stellar;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Search;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar;

/// <summary>
/// NCBC-4269 / RFC 77 CNG-1: WaitUntilReady pings the standard gRPC health-check RPC and succeeds
/// only when the server reports SERVING, retrying otherwise. The connection-failure/timeout paths
/// (bad host, server down, no TLS, bad creds → UnambiguousTimeoutException) are covered live/FIT.
/// </summary>
public class StellarWaitUntilReadyTests
{
    private static readonly HealthCheckResponse Serving =
        new() { Status = HealthCheckResponse.Types.ServingStatus.Serving };
    private static readonly HealthCheckResponse NotServing =
        new() { Status = HealthCheckResponse.Types.ServingStatus.NotServing };

    private static AsyncUnaryCall<HealthCheckResponse> UnaryCall(HealthCheckResponse response) =>
        new(Task.FromResult(response), Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess, () => new Metadata(), () => { });

    private static StellarCluster BuildCluster(Health.HealthClient healthClient)
    {
        var requestTracer = new Mock<IRequestTracer>();
        requestTracer.Setup(x => x.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>()))
            .Returns(new NoopRequestSpan());

        // Real StellarRetryHandler so the retry/serving-status logic actually runs.
        var cluster = new StellarCluster(
            Mock.Of<IBucketManager>(), Mock.Of<ISearchIndexManager>(), Mock.Of<IQueryIndexManager>(),
            Mock.Of<IQueryClient>(), Mock.Of<IAnalyticsClient>(), Mock.Of<IStellarSearchClient>(),
            new Metadata(), requestTracer.Object, GrpcChannel.ForAddress("https://localhost"),
            Mock.Of<ITypeSerializer>(), new StellarRetryHandler(), new ClusterOptions(),
            Mock.Of<IOperationCompressor>());
        cluster.HealthClient = healthClient;
        return cluster;
    }

    [Fact]
    public async Task Serving_Completes()
    {
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        var cluster = BuildCluster(health.Object);

        await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Once);
    }

    [Fact]
    public async Task NonServing_IsRetriedUntilServing()
    {
        var health = new Mock<Health.HealthClient>();
        health.SetupSequence(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(NotServing))
            .Returns(UnaryCall(NotServing))
            .Returns(UnaryCall(Serving));

        var cluster = BuildCluster(health.Object);

        // A non-SERVING status is retryable, not fatal — it retries and then succeeds.
        await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ServiceTypesAndDesiredState_AreSilentlyIgnored()
    {
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        var cluster = BuildCluster(health.Object);

        // Per RFC these options are ignored (not honored, not rejected): the call still succeeds.
        var options = new WaitUntilReadyOptions()
            .ServiceTypes(ServiceType.KeyValue, ServiceType.Query)
            .DesiredState(ClusterState.Offline);

        await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5), options);
    }

    [Fact]
    public async Task Bucket_DelegatesToClusterHealthCheck()
    {
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        var cluster = BuildCluster(health.Object);
        var bucket = new StellarBucket("default", cluster);

        await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(5));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Once);
    }

    [Fact]
    public async Task PersistentlyNotServing_TimesOut_Unambiguously()
    {
        // The core WaitUntilReady guarantee: a server that never reaches SERVING must fail with
        // UnambiguousTimeoutException once the timeout elapses, not retry forever.
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(NotServing));

        var cluster = BuildCluster(health.Object);

        await Assert.ThrowsAsync<UnambiguousTimeoutException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public async Task ZeroTimeout_ThrowsUnambiguousTimeout_WithoutCallingHealthCheck()
    {
        // A non-positive timeout has already elapsed (matches the classic cluster contract) and must
        // short-circuit before any health check — this is the regression guard for the unbounded
        // retry loop a zero timeout previously caused (null RemainingTimeout => no gRPC deadline).
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        var cluster = BuildCluster(health.Object);

        await Assert.ThrowsAsync<UnambiguousTimeoutException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.Zero));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }

    [Fact]
    public async Task CancelledToken_ShortCircuits_WithoutCallingHealthCheck()
    {
        // A caller token that is already cancelled aborts before any health check. Stellar maps a
        // cancelled retry loop to UnambiguousTimeoutException uniformly (see StellarRetryHandler).
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        var cluster = BuildCluster(health.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var options = new WaitUntilReadyOptions().CancellationToken(cts.Token);

        await Assert.ThrowsAsync<UnambiguousTimeoutException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5), options));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }
}
#endif
