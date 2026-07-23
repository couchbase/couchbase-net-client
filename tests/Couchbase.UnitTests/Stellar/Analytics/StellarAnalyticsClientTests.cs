#if NETCOREAPP3_1_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
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
}
#endif
