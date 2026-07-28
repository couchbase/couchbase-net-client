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
using Microsoft.Extensions.Time.Testing;
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

    /// <summary>
    /// Builds a cluster whose timeout budget, backoff delays and request clock all run on
    /// <paramref name="timeProvider"/>. Tests pass a <see cref="FakeTimeProvider"/> so the retry loop
    /// is driven by explicit clock advances rather than the wall clock — on a loaded CI runner a real
    /// 5s budget can expire during a backoff that nominally costs 60ms.
    /// </summary>
    private static StellarCluster BuildCluster(Health.HealthClient healthClient, TimeProvider timeProvider)
    {
        var requestTracer = new Mock<IRequestTracer>();
        requestTracer.Setup(x => x.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>()))
            .Returns(new NoopRequestSpan());

        // Real StellarRetryHandler so the retry/serving-status logic actually runs.
        var cluster = new StellarCluster(
            Mock.Of<IBucketManager>(), Mock.Of<ISearchIndexManager>(), Mock.Of<IQueryIndexManager>(),
            Mock.Of<IQueryClient>(), Mock.Of<IAnalyticsClient>(), Mock.Of<IStellarSearchClient>(),
            new Metadata(), requestTracer.Object, GrpcChannel.ForAddress("https://localhost"),
            Mock.Of<ITypeSerializer>(), new StellarRetryHandler(timeProvider), new ClusterOptions(),
            Mock.Of<IOperationCompressor>(), timeProvider: timeProvider);
        cluster.HealthClient = healthClient;
        return cluster;
    }

    private static FakeTimeProvider NewFakeTime()
    {
        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);
        return fakeTime;
    }

    /// <summary>
    /// Runs <paramref name="operation"/> on a background task and advances <paramref name="fakeTime"/> in
    /// <paramref name="stepMs"/> increments until it completes, then returns the (completed) task for the
    /// caller to await. The loop is bounded so a regression that never completes fails the test instead of
    /// hanging the suite.
    /// <para>
    /// The pump advances the clock at a rate set by real scheduling, so for a test that must NOT time out,
    /// keep <c>stepMs * maxAdvances</c> comfortably below the WaitUntilReady budget — otherwise a slow
    /// runner could pump the fake clock past the deadline and reintroduce the very flake this avoids.
    /// </para>
    /// </summary>
    private static async Task<Task> PumpAsync(FakeTimeProvider fakeTime, Func<Task> operation,
        int stepMs = 10, int maxAdvances = 200)
    {
        var task = Task.Run(operation);
        var step = TimeSpan.FromMilliseconds(stepMs);

        for (var i = 0; i < maxAdvances && !task.IsCompleted; i++)
        {
            fakeTime.Advance(step);
            await Task.Delay(1); // real yield so the operation can register its next timer
        }

        Assert.True(task.IsCompleted,
            $"Operation did not complete after {maxAdvances} advances of {step} on the fake clock.");
        return task;
    }

    [Fact]
    public async Task Serving_Completes()
    {
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        // No retries and no backoff, so the fake clock never advances: the 5s budget cannot expire.
        var cluster = BuildCluster(health.Object, NewFakeTime());

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

        var fakeTime = NewFakeTime();
        var cluster = BuildCluster(health.Object, fakeTime);

        // A non-SERVING status is retryable, not fatal — it retries and then succeeds. The two backoffs
        // (10ms + 50ms) are driven by advancing the fake clock; the pump advances at most 200 x 10ms = 2s,
        // so the 5s budget cannot expire however slowly the runner schedules the pump.
        var waitUntilReady = await PumpAsync(fakeTime, () => cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5)));
        await waitUntilReady;

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ServiceTypesAndDesiredState_AreSilentlyIgnored()
    {
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        var cluster = BuildCluster(health.Object, NewFakeTime());

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

        var cluster = BuildCluster(health.Object, NewFakeTime());
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

        var fakeTime = NewFakeTime();
        var cluster = BuildCluster(health.Object, fakeTime);

        // 100ms steps so the fake clock outruns the growing backoff ladder and reaches the 500ms budget.
        var waitUntilReady = await PumpAsync(fakeTime,
            () => cluster.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(500)), stepMs: 100);

        await Assert.ThrowsAsync<UnambiguousTimeoutException>(() => waitUntilReady);
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

        var cluster = BuildCluster(health.Object, NewFakeTime());

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

        var cluster = BuildCluster(health.Object, NewFakeTime());

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var options = new WaitUntilReadyOptions().CancellationToken(cts.Token);

        await Assert.ThrowsAsync<UnambiguousTimeoutException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5), options));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }
}
#endif
