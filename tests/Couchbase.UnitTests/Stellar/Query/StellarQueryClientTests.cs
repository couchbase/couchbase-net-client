#if NETCOREAPP3_1_OR_GREATER
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Query;
using Couchbase.UnitTests.Stellar.Utils;
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
}
#endif
