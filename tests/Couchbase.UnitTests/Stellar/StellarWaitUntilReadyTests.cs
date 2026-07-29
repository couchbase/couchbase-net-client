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
    /// Builds a cluster whose timeout budget, backoff waits and request clock all run on a
    /// <see cref="FakeTimeProvider"/>, with the retry handler's backoff replaced by a delegate that
    /// advances that clock by the backoff and returns synchronously.
    /// <para>
    /// So a retry costs the timeout budget exactly what it costs in production, but there is no timer to
    /// wait on and nothing to poll: WaitUntilReady runs to completion on the calling thread, and no test
    /// here depends on the scheduler. That matters — a wall-clock budget racing a real backoff failed on
    /// CI once (NonServing_IsRetriedUntilServing, 5s budget, 7.5s wall), and pumping the fake clock from
    /// a background task in its place failed the same way one layer up, exhausting its bounded advances
    /// before the operation was scheduled.
    /// </para>
    /// </summary>
    private static StellarCluster BuildCluster(Health.HealthClient healthClient)
    {
        var requestTracer = new Mock<IRequestTracer>();
        requestTracer.Setup(x => x.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>()))
            .Returns(new NoopRequestSpan());

        var fakeTime = new FakeTimeProvider();
        fakeTime.SetUtcNow(DateTimeOffset.UtcNow);

        // Real StellarRetryHandler so the retry/serving-status logic actually runs.
        var retryHandler = new StellarRetryHandler(fakeTime)
        {
            Delay = (backoff, _) =>
            {
                fakeTime.Advance(backoff);
                return Task.CompletedTask;
            }
        };

        var cluster = new StellarCluster(
            Mock.Of<IBucketManager>(), Mock.Of<ISearchIndexManager>(), Mock.Of<IQueryIndexManager>(),
            Mock.Of<IQueryClient>(), Mock.Of<IAnalyticsClient>(), Mock.Of<IStellarSearchClient>(),
            new Metadata(), requestTracer.Object, GrpcChannel.ForAddress("https://localhost"),
            Mock.Of<ITypeSerializer>(), retryHandler, new ClusterOptions(),
            Mock.Of<IOperationCompressor>(), timeProvider: fakeTime);
        cluster.HealthClient = healthClient;
        return cluster;
    }

    [Fact]
    public async Task Serving_Completes()
    {
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        // No retries and no backoff, so the fake clock never advances: the 5s budget cannot expire.
        using var cluster = BuildCluster(health.Object);

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

        using var cluster = BuildCluster(health.Object);

        // A non-SERVING status is retryable, not fatal — it retries and then succeeds. The two backoffs
        // (10ms + 50ms) charge the fake clock 60ms of the 5s budget, so the wait cannot time out.
        await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ServiceTypesAndDesiredState_AreSilentlyIgnored()
    {
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        using var cluster = BuildCluster(health.Object);

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

        using var cluster = BuildCluster(health.Object);
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
        var calls = 0;
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(() =>
            {
                // The loop is bounded by the budget, which the injected backoff charges (10 + 50 + 100 +
                // 500ms crosses 500ms on the fourth attempt). This fails the test rather than spinning
                // forever if a regression ever unbinds it — the retries no longer cost real time.
                Assert.True(++calls <= 50, "WaitUntilReady kept retrying past its timeout budget.");
                return UnaryCall(NotServing);
            });

        using var cluster = BuildCluster(health.Object);

        var exception = await Assert.ThrowsAsync<UnambiguousTimeoutException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.FromMilliseconds(500)));

        // The status that caused the wait to fail has to reach the user: the RpcException that drives
        // the retry is swallowed by the retry loop, so a bare "timed out" leaves them with nothing to
        // go on. Whichever of the three places spots the timeout, the message reads the same.
        Assert.StartsWith("Timed out after 00:00:00.5000000.", exception.Message);
        Assert.Contains(nameof(HealthCheckResponse.Types.ServingStatus.NotServing), exception.Message);
    }

    [Fact]
    public async Task DisposedBucket_ThrowsObjectDisposed()
    {
        // WaitUntilReady delegates to the cluster, which is still alive — so without the bucket's own
        // disposal check a disposed bucket would happily report ready.
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        using var cluster = BuildCluster(health.Object);
        var bucket = new StellarBucket("default", cluster);
        bucket.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(5)));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Never);
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

        using var cluster = BuildCluster(health.Object);

        await Assert.ThrowsAsync<UnambiguousTimeoutException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.Zero));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }

    [Fact]
    public async Task CancelledToken_ShortCircuits_WithoutCallingHealthCheck()
    {
        // A caller token that is already cancelled aborts before any health check, and surfaces as
        // OperationCanceledException — the same contract as the classic cluster, which rethrows
        // external cancellation instead of mapping it to a timeout.
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        using var cluster = BuildCluster(health.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var options = new WaitUntilReadyOptions().CancellationToken(cts.Token);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5), options));
        Assert.Equal(cts.Token, exception.CancellationToken);

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }

    [Fact]
    public async Task CallerToken_IsForwardedToTheHealthCheckCall()
    {
        // The token has to reach the gRPC call itself, otherwise cancelling only takes effect between
        // attempts and an in-flight health check keeps running to the deadline.
        var health = new Mock<Health.HealthClient>();
        CallOptions captured = default;
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Callback((HealthCheckRequest _, CallOptions callOptions) => captured = callOptions)
            .Returns(UnaryCall(Serving));

        using var cluster = BuildCluster(health.Object);

        using var cts = new CancellationTokenSource();
        await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5),
            new WaitUntilReadyOptions().CancellationToken(cts.Token));

        Assert.Equal(cts.Token, captured.CancellationToken);
    }

    [Fact]
    public async Task TokenCancelledWhileRetrying_ThrowsOperationCanceled_NotTimeout()
    {
        // Cancelling mid-wait must break out of the retry loop as cancellation. Without the
        // normalisation in WaitUntilReadyAsync this reports UnambiguousTimeoutException, because the
        // retry loop maps any cancelled request token to a timeout.
        var health = new Mock<Health.HealthClient>();
        using var cts = new CancellationTokenSource();
        var calls = 0;
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(() =>
            {
                if (++calls == 2)
                {
                    cts.Cancel();
                }

                return UnaryCall(NotServing);
            });

        using var cluster = BuildCluster(health.Object);
        var options = new WaitUntilReadyOptions().CancellationToken(cts.Token);

        // A 30s budget against two backoffs charging 60ms, so the only thing that can end this wait is
        // the caller's token.
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(30), options));
        Assert.Equal(cts.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task GrpcCancelledOnCallerToken_ThrowsOperationCanceled_NotRequestCanceled()
    {
        // What actually happens on the wire when the caller cancels an in-flight health check: gRPC
        // aborts the call with CANCELLED, which the retry handler maps to RequestCanceledException.
        // WaitUntilReady reports the caller's cancellation instead.
        var health = new Mock<Health.HealthClient>();
        using var cts = new CancellationTokenSource();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Callback(() => cts.Cancel())
            .Throws(new RpcException(new Status(StatusCode.Cancelled, "cancelled by the caller")));

        using var cluster = BuildCluster(health.Object);
        var options = new WaitUntilReadyOptions().CancellationToken(cts.Token);

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(5), options));
        Assert.Equal(cts.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Bucket_HonoursCallerToken()
    {
        // The bucket delegates to the cluster, so the options token has to survive the hop.
        var health = new Mock<Health.HealthClient>();
        health.Setup(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()))
            .Returns(UnaryCall(Serving));

        using var cluster = BuildCluster(health.Object);
        var bucket = new StellarBucket("default", cluster);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(5),
                new WaitUntilReadyOptions().CancellationToken(cts.Token)));

        health.Verify(h => h.CheckAsync(It.IsAny<HealthCheckRequest>(), It.IsAny<CallOptions>()), Times.Never);
    }
}
#endif
