#if NETCOREAPP3_1_OR_GREATER
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Protostellar.Analytics.V1;
using Couchbase.Stellar.Analytics;
using Couchbase.Stellar.Core.Retry;
using Couchbase.UnitTests.Stellar.Utils;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Analytics;

public class StellarAnalyticsClientTests
{
    private static AsyncServerStreamingCall<AnalyticsQueryResponse> Call(IAsyncStreamReader<AnalyticsQueryResponse> stream) =>
        new(stream, null!, null!, null!, null!, null!);

    // A streamed response with just enough metadata for ProtoAnalyticsResult to process it.
    private static AnalyticsQueryResponse ResponseWithMetaData() => new()
    {
        MetaData = new AnalyticsQueryResponse.Types.MetaData
        {
            Metrics = new AnalyticsQueryResponse.Types.Metrics
            {
                ElapsedTime = new Duration(),
                ExecutionTime = new Duration()
            }
        }
    };

    // NCBC-4263 regression: a retryable error on the FIRST streamed response (before any rows are
    // delivered) must be retried per RFC 77 — not converted to a non-retryable RequestCanceled.
    // Before the fix, analytics read the first response outside the retry loop and routed the error
    // through the mid-stream (never-retry) path. Query already did this correctly.
    [Fact]
    public async Task QueryAsync_RetryableFirstResponseError_IsRetried()
    {
        var cluster = StellarMocks.CreateClusterFromMocks();
        var serviceClient = new Mock<AnalyticsService.AnalyticsServiceClient>();

        // First attempt: the first stream read fails with a retryable error.
        var failingStream = new Mock<IAsyncStreamReader<AnalyticsQueryResponse>>();
        failingStream
            .Setup(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "Service unavailable")));

        // Retry attempt: succeeds (empty stream — no responses).
        var okStream = new Mock<IAsyncStreamReader<AnalyticsQueryResponse>>();
        okStream.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        serviceClient
            .SetupSequence(x => x.AnalyticsQuery(It.IsAny<AnalyticsQueryRequest>(), It.IsAny<CallOptions>()))
            .Returns(Call(failingStream.Object))
            .Returns(Call(okStream.Object));

        var client = new StellarAnalyticsClient(cluster, serviceClient.Object, new StellarRetryHandler());

        // Before the fix this threw RequestCanceledException; now the first-response error is retried.
        var result = await client.QueryAsync<dynamic>("SELECT 1;", new AnalyticsOptions());

        Assert.NotNull(result);
        serviceClient.Verify(
            x => x.AnalyticsQuery(It.IsAny<AnalyticsQueryRequest>(), It.IsAny<CallOptions>()),
            Times.Exactly(2)); // proves the first-response error was retried, not surfaced as terminal
    }

    /// <summary>
    /// RFC 67: the stable db.query.text is set only when positional or named parameters are in use, because
    /// without parameters the statement can contain sensitive values. The legacy db.statement is always set.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task QueryAsync_StatementAttribute_StableNameOnlyWithParameters(bool named, bool positional)
    {
        const string statement = "SELECT * FROM `default` WHERE secret = 'value'";

        var innerSpan = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
        innerSpan.Setup(s => s.CanWrite).Returns(true);
        var span = new RequestSpanWrapper(innerSpan.Object, null, ObservabilitySemanticConvention.Both);
        var tracer = new Mock<IRequestTracer>();
        tracer.Setup(t => t.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>())).Returns(span);

        var cluster = StellarMocks.CreateClusterFromMocks(tracer: tracer.Object);
        var serviceClient = new Mock<AnalyticsService.AnalyticsServiceClient>();
        var stream = new Mock<IAsyncStreamReader<AnalyticsQueryResponse>>();
        stream.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        serviceClient
            .Setup(x => x.AnalyticsQuery(It.IsAny<AnalyticsQueryRequest>(), It.IsAny<CallOptions>()))
            .Returns(Call(stream.Object));
        var client = new StellarAnalyticsClient(cluster, serviceClient.Object, new StellarRetryHandler());

        var options = new AnalyticsOptions();
        if (named) options.Parameter("name", "a");
        if (positional) options.Parameter("a");
        await client.QueryAsync<dynamic>(statement, options);

        innerSpan.Verify(s => s.SetAttribute("db.statement", statement), Times.Once());
        innerSpan.Verify(s => s.SetAttribute("db.query.text", statement),
            named || positional ? Times.Once() : Times.Never());
    }

    // The other half: an error that occurs AFTER the first response (genuinely mid-stream) still maps
    // to RequestCanceled, because rows may already have been delivered and retrying isn't safe.
    [Fact]
    public async Task QueryAsync_MidStreamError_ThrowsRequestCanceled()
    {
        var cluster = StellarMocks.CreateClusterFromMocks();
        var serviceClient = new Mock<AnalyticsService.AnalyticsServiceClient>();

        var stream = new Mock<IAsyncStreamReader<AnalyticsQueryResponse>>();
        stream
            .SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true) // first response succeeds (read inside the retry loop)
            .ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"))); // mid-stream
        stream.Setup(x => x.Current).Returns(ResponseWithMetaData());

        serviceClient
            .Setup(x => x.AnalyticsQuery(It.IsAny<AnalyticsQueryRequest>(), It.IsAny<CallOptions>()))
            .Returns(Call(stream.Object));

        var client = new StellarAnalyticsClient(cluster, serviceClient.Object, new StellarRetryHandler());

        // The first response succeeds, so QueryAsync returns; the mid-stream failure surfaces on enumeration.
        var result = await client.QueryAsync<dynamic>("SELECT 1;", new AnalyticsOptions());

        await Assert.ThrowsAsync<RequestCanceledException>(async () =>
        {
            await foreach (var _ in result.Rows) { }
        });

        // The mid-stream error is NOT retried.
        serviceClient.Verify(
            x => x.AnalyticsQuery(It.IsAny<AnalyticsQueryRequest>(), It.IsAny<CallOptions>()),
            Times.Once);
    }

    /// <summary>
    /// As for query (RFC 77): the analytics span ends only after the response stream closes.
    /// The stream has a first response and one more response with a row, then ends. The span counts
    /// End and Dispose calls together as "ended". With an <see cref="ActivitySource"/>, the span also
    /// starts a real activity when the query starts and stops it when the span ends.
    /// </summary>
    private sealed class SpanLifetimeFixture
    {
        public int Ended;
        public RequestSpanStatusCode? Status;
        public Activity Activity;
        public readonly Mock<IAsyncStreamReader<AnalyticsQueryResponse>> Stream = new();
        public readonly StellarAnalyticsClient Client;

        public SpanLifetimeFixture(ActivitySource source = null)
        {
            var innerSpan = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
            innerSpan.Setup(s => s.End()).Callback(() => { Ended++; Activity?.Stop(); });
            innerSpan.Setup(s => s.Dispose()).Callback(() => { Ended++; Activity?.Stop(); });
            innerSpan.Setup(s => s.SetStatus(It.IsAny<RequestSpanStatusCode>()))
                .Callback<RequestSpanStatusCode>(code => Status = code)
                .Returns(innerSpan.Object);
            var tracer = new Mock<IRequestTracer>();
            tracer.Setup(t => t.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>()))
                .Returns(() =>
                {
                    Activity = source?.StartActivity("analytics");
                    return innerSpan.Object;
                });

            var response = ResponseWithMetaData();
            response.Rows.Add(Google.Protobuf.ByteString.CopyFromUtf8("1"));
            Stream.Setup(x => x.Current).Returns(response);
            Stream.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            var service = new Mock<AnalyticsService.AnalyticsServiceClient>();
            service.Setup(x => x.AnalyticsQuery(It.IsAny<AnalyticsQueryRequest>(), It.IsAny<CallOptions>()))
                .Returns(new AsyncServerStreamingCall<AnalyticsQueryResponse>(Stream.Object, null!, null!, null!, _ => { }, null!));

            Client = new StellarAnalyticsClient(StellarMocks.CreateClusterFromMocks(tracer: tracer.Object),
                service.Object, new StellarRetryHandler());
        }
    }

    [Fact]
    public async Task QueryAsync_Span_EndsWhenTheStreamIsReadToTheEnd()
    {
        var f = new SpanLifetimeFixture();

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        Assert.Equal(0, f.Ended);

        var rows = 0;
        await foreach (var _ in result.Rows)
        {
            rows++;
            Assert.Equal(0, f.Ended);
        }

        Assert.Equal(2, rows);
        Assert.Equal(1, f.Ended);
        Assert.Equal(RequestSpanStatusCode.Ok, f.Status);

        result.Dispose();
        Assert.Equal(1, f.Ended);
    }

    [Fact]
    public async Task QueryAsync_Span_EndsBeforeQueryAsyncReturns_WhenTheStreamIsEmpty()
    {
        var f = new SpanLifetimeFixture();
        f.Stream.Setup(x => x.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        Assert.Equal(1, f.Ended);

        await foreach (var _ in result.Rows) { }
        result.Dispose();
        Assert.Equal(1, f.Ended);
    }

    [Fact]
    public async Task QueryAsync_Span_EndsOnDispose_WhenTheCallerStopsReadingEarly()
    {
        var f = new SpanLifetimeFixture();

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        await foreach (var _ in result.Rows)
        {
            break;
        }
        Assert.Equal(0, f.Ended);

        result.Dispose();
        Assert.Equal(1, f.Ended);
    }

    [Fact]
    public async Task QueryAsync_Span_EndsWhenTheStreamFailsMidStream()
    {
        var f = new SpanLifetimeFixture();
        f.Stream.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ThrowsAsync(new RpcException(new Status(StatusCode.Internal, "mid-stream")));

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        await Assert.ThrowsAnyAsync<System.Exception>(async () =>
        {
            await foreach (var _ in result.Rows) { }
        });

        Assert.Equal(1, f.Ended);
        Assert.Equal(RequestSpanStatusCode.Error, f.Status);
    }

    /// <summary>
    /// Activity.Stop sets Activity.Current to the activity that was current when the stopped one
    /// started. Disposing the result must not replace the activity the caller started after the query.
    /// </summary>
    [Fact]
    public async Task QueryAsync_Span_DisposeKeepsTheCallersCurrentActivity()
    {
        using var source = new ActivitySource(nameof(StellarAnalyticsClientTests) + nameof(QueryAsync_Span_DisposeKeepsTheCallersCurrentActivity));
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => ReferenceEquals(s, source),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        var f = new SpanLifetimeFixture(source);

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        using var callers = source.StartActivity("callers");
        Assert.NotNull(callers);

        result.Dispose();

        Assert.Equal(1, f.Ended);
        Assert.NotEqual(System.TimeSpan.Zero, f.Activity!.Duration);
        Assert.Same(callers, Activity.Current);
    }
}
#endif
