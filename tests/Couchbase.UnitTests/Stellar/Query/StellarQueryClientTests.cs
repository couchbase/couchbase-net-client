#if NETCOREAPP3_1_OR_GREATER
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Query;
using Couchbase.UnitTests.Stellar.Utils;
using Google.Protobuf;
using Grpc.Core;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Query;

public class StellarQueryClientTests
{
    [Fact]
    public async Task Test_QueryAsync()
    {
        var cluster = StellarMocks.CreateClusterFromMocks();
        var queryServiceClient = new Mock<QueryService.QueryServiceClient>();

        var asyncStreamReader = new Mock<IAsyncStreamReader<QueryResponse>>();
        asyncStreamReader.Setup(x => x.Current).Returns(new QueryResponse());
        var asyncServerStreamingCall = new AsyncServerStreamingCall<QueryResponse>
            (asyncStreamReader.Object, null, null, null, null, null);

        queryServiceClient.Setup(x=>x.Query(It.IsAny<QueryRequest>(), It.IsAny<CallOptions>())).Returns(asyncServerStreamingCall);
        var retryHandler = new StellarRetryHandler();
        var typeSerializer = SystemTextJsonSerializer.Create();

        var queryClient = new StellarQueryClient(cluster, queryServiceClient.Object, typeSerializer, retryHandler);
        var result = await queryClient.QueryAsync<dynamic>("SELECT 1;", new QueryOptions());

        await result.GetAsyncEnumerator().MoveNextAsync();
    }

    /// <summary>
    /// RFC 67: the stable db.query.text is set only when positional or named parameters are in use, because
    /// without parameters the statement can contain sensitive values. The legacy db.statement is always set.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryAsync_StatementAttribute_StableNameOnlyWithParameters(bool hasParameters)
    {
        const string statement = "SELECT * FROM `default` WHERE secret = 'value'";

        var innerSpan = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
        innerSpan.Setup(s => s.CanWrite).Returns(true);
        var span = new RequestSpanWrapper(innerSpan.Object, null, ObservabilitySemanticConvention.Both);
        var tracer = new Mock<IRequestTracer>();
        tracer.Setup(t => t.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>())).Returns(span);

        var cluster = StellarMocks.CreateClusterFromMocks(tracer: tracer.Object);
        var queryServiceClient = new Mock<QueryService.QueryServiceClient>();
        var asyncStreamReader = new Mock<IAsyncStreamReader<QueryResponse>>();
        asyncStreamReader.Setup(x => x.Current).Returns(new QueryResponse());
        queryServiceClient.Setup(x => x.Query(It.IsAny<QueryRequest>(), It.IsAny<CallOptions>()))
            .Returns(new AsyncServerStreamingCall<QueryResponse>(asyncStreamReader.Object, null, null, null, null, null));
        var queryClient = new StellarQueryClient(cluster, queryServiceClient.Object,
            SystemTextJsonSerializer.Create(), new StellarRetryHandler());

        var options = hasParameters ? new QueryOptions().Parameter("name", "a") : new QueryOptions();
        await queryClient.QueryAsync<dynamic>(statement, options);

        innerSpan.Verify(s => s.SetAttribute("db.statement", statement), Times.Once());
        innerSpan.Verify(s => s.SetAttribute("db.query.text", statement),
            hasParameters ? Times.Once() : Times.Never());
    }

    // CNG-2 regression: query idempotency (and thus timeout ambiguity) must track the ReadOnly option.
    // A mutating query that times out may have applied, so it must surface as Ambiguous; a read-only
    // one definitively did not, so it is Unambiguous.
    [Theory]
    [InlineData(true, typeof(Couchbase.Core.Exceptions.UnambiguousTimeoutException))]
    [InlineData(false, typeof(Couchbase.Core.Exceptions.AmbiguousTimeoutException))]
    public async Task QueryAsync_TimeoutAmbiguity_TracksReadOnlyOption(bool readOnly, System.Type expected)
    {
        var cluster = StellarMocks.CreateClusterFromMocks();
        var queryServiceClient = new Mock<QueryService.QueryServiceClient>();
        queryServiceClient
            .Setup(x => x.Query(It.IsAny<QueryRequest>(), It.IsAny<CallOptions>()))
            .Throws(new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline exceeded")));

        var queryClient = new StellarQueryClient(cluster, queryServiceClient.Object,
            SystemTextJsonSerializer.Create(), new StellarRetryHandler());

        var ex = await Assert.ThrowsAnyAsync<System.Exception>(
            () => queryClient.QueryAsync<dynamic>("SELECT 1;", new QueryOptions().Readonly(readOnly)));

        Assert.IsType(expected, ex);
    }

    /// <summary>
    /// A query whose stream has a first response and one more response with a row, then ends.
    /// The span counts End and Dispose calls together as "ended".
    /// </summary>
    private sealed class SpanLifetimeFixture
    {
        public int Ended;
        public readonly Mock<IAsyncStreamReader<QueryResponse>> Stream = new();
        public readonly Mock<QueryService.QueryServiceClient> Service = new();
        public readonly StellarQueryClient Client;

        public SpanLifetimeFixture()
        {
            var innerSpan = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
            innerSpan.Setup(s => s.End()).Callback(() => Ended++);
            innerSpan.Setup(s => s.Dispose()).Callback(() => Ended++);
            var tracer = new Mock<IRequestTracer>();
            tracer.Setup(t => t.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>())).Returns(innerSpan.Object);

            var response = new QueryResponse();
            response.Rows.Add(ByteString.CopyFromUtf8("1"));
            Stream.Setup(x => x.Current).Returns(response);
            Stream.SetupSequence(x => x.MoveNext(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)   // first response, read before QueryAsync returns
                .ReturnsAsync(true)   // second response
                .ReturnsAsync(false); // end of stream
            Service.Setup(x => x.Query(It.IsAny<QueryRequest>(), It.IsAny<CallOptions>()))
                .Returns(new AsyncServerStreamingCall<QueryResponse>(Stream.Object, null!, null!, null!, _ => { }, null!));

            Client = new StellarQueryClient(StellarMocks.CreateClusterFromMocks(tracer: tracer.Object),
                Service.Object, SystemTextJsonSerializer.Create(), new StellarRetryHandler());
        }
    }

    /// <summary>
    /// RFC 77: the query span ends only after the response stream closes, so that the gRPC span
    /// does not outlive it.
    /// </summary>
    [Fact]
    public async Task QueryAsync_Span_EndsWhenTheStreamIsReadToTheEnd()
    {
        var f = new SpanLifetimeFixture();

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new QueryOptions());
        Assert.Equal(0, f.Ended);

        var rows = 0;
        await foreach (var _ in result.Rows)
        {
            rows++;
            Assert.Equal(0, f.Ended);
        }

        Assert.Equal(2, rows);
        Assert.Equal(1, f.Ended);

        result.Dispose();
        Assert.Equal(1, f.Ended);
    }

    [Fact]
    public async Task QueryAsync_Span_EndsOnDispose_WhenTheCallerStopsReadingEarly()
    {
        var f = new SpanLifetimeFixture();

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new QueryOptions());
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

        var result = await f.Client.QueryAsync<int>("SELECT 1;", new QueryOptions());
        await Assert.ThrowsAnyAsync<System.Exception>(async () =>
        {
            await foreach (var _ in result.Rows) { }
        });

        Assert.Equal(1, f.Ended);
    }

    [Fact]
    public async Task QueryAsync_Span_EndsWhenTheQueryFails()
    {
        var f = new SpanLifetimeFixture();
        f.Service.Setup(x => x.Query(It.IsAny<QueryRequest>(), It.IsAny<CallOptions>()))
            .Throws(new RpcException(new Status(StatusCode.InvalidArgument, "bad statement")));

        await Assert.ThrowsAnyAsync<System.Exception>(() => f.Client.QueryAsync<int>("SELECT 1;", new QueryOptions()));

        Assert.Equal(1, f.Ended);
    }
}
#endif
