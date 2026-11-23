#if NETCOREAPP3_1_OR_GREATER
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
    /// The span counts End and Dispose calls together as "ended".
    /// </summary>
    private static (StellarAnalyticsClient Client, Mock<IAsyncStreamReader<AnalyticsQueryResponse>> Stream, System.Func<int> Ended) SpanLifetimeFixture()
    {
        var ended = 0;
        var innerSpan = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
        innerSpan.Setup(s => s.End()).Callback(() => ended++);
        innerSpan.Setup(s => s.Dispose()).Callback(() => ended++);
        var tracer = new Mock<IRequestTracer>();
        tracer.Setup(t => t.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>())).Returns(innerSpan.Object);

        var response = ResponseWithMetaData();
        response.Rows.Add(Google.Protobuf.ByteString.CopyFromUtf8("1"));
        var stream = new Mock<IAsyncStreamReader<AnalyticsQueryResponse>>();
        stream.Setup(x => x.Current).Returns(response);
        stream.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        var service = new Mock<AnalyticsService.AnalyticsServiceClient>();
        service.Setup(x => x.AnalyticsQuery(It.IsAny<AnalyticsQueryRequest>(), It.IsAny<CallOptions>()))
            .Returns(new AsyncServerStreamingCall<AnalyticsQueryResponse>(stream.Object, null!, null!, null!, _ => { }, null!));

        var client = new StellarAnalyticsClient(StellarMocks.CreateClusterFromMocks(tracer: tracer.Object),
            service.Object, new StellarRetryHandler());
        return (client, stream, () => ended);
    }

    [Fact]
    public async Task QueryAsync_Span_EndsWhenTheStreamIsReadToTheEnd()
    {
        var (client, _, ended) = SpanLifetimeFixture();

        var result = await client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        Assert.Equal(0, ended());

        var rows = 0;
        await foreach (var _ in result.Rows)
        {
            rows++;
            Assert.Equal(0, ended());
        }

        Assert.Equal(2, rows);
        Assert.Equal(1, ended());

        result.Dispose();
        Assert.Equal(1, ended());
    }

    [Fact]
    public async Task QueryAsync_Span_EndsOnDispose_WhenTheCallerStopsReadingEarly()
    {
        var (client, _, ended) = SpanLifetimeFixture();

        var result = await client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        await foreach (var _ in result.Rows)
        {
            break;
        }
        Assert.Equal(0, ended());

        result.Dispose();
        Assert.Equal(1, ended());
    }

    [Fact]
    public async Task QueryAsync_Span_EndsWhenTheStreamFailsMidStream()
    {
        var (client, stream, ended) = SpanLifetimeFixture();
        stream.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ThrowsAsync(new RpcException(new Status(StatusCode.Internal, "mid-stream")));

        var result = await client.QueryAsync<int>("SELECT 1;", new AnalyticsOptions());
        await Assert.ThrowsAnyAsync<System.Exception>(async () =>
        {
            await foreach (var _ in result.Rows) { }
        });

        Assert.Equal(1, ended());
    }
}
#endif
