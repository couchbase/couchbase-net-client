#if NETCOREAPP3_1_OR_GREATER
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Protostellar.KV.V1;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core.Retry;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Core;

public class StellarRetryHandlerTests
{
    [Theory]
    [InlineData( "DocumentNotFound", StatusCode.NotFound, "\n\bdocument\u0012\"default/_default/_default/fake_doc")]
    [InlineData( "CollectionExists", StatusCode.AlreadyExists, "\n\ncollection\u0012\u0017default/_default/5efcb8")]
    [InlineData( "IndexExists", StatusCode.AlreadyExists, "\n\nqueryindex\u0012\b#primary")]
    public async Task RetryThrowsTypeUrlResourceErrors(string expectedException, StatusCode statusCode, string detailValue)
    {
        var any = new Any
        {
            Value = ByteString.CopyFromUtf8(detailValue),
            TypeUrl = StellarRetryStrings.TypeUrlResourceInfo
        };
        var retryMock = new Mock<StellarRetryHandler>();
        retryMock.Setup(handler => handler.StatusDeserializer(It.IsAny<RpcException>())).Returns(any);

        var grpcCall = () =>
        {
            var thrower = () => { throw new RpcException(new Status(statusCode, "NoDetail")); };
            thrower.Invoke();
            return Task.FromResult(new GetResponse());
        };

        var result = await Record.ExceptionAsync( () => retryMock.Object.RetryAsync(grpcCall, new StellarRequest()));
        Debug.Assert(result != null, nameof(result) + " != null");
        Assert.Contains(expectedException, result.ToString());
    }

    [Fact]
    public async Task Throw_CouchbaseException_On_Unknown_Error()
    {
        var retryMock = new StellarRetryHandler();
        var request = new StellarRequest();
        var responseMock = new Mock<IQueryResult<object>>();
        // ReSharper disable once NotDisposedResourceIsReturned
        responseMock.Setup(x => x.GetAsyncEnumerator(new CancellationToken())).Throws<CouchbaseException>();

        Task<IQueryResult<object>> GrpcCall()
        {
            throw new RpcException(new Status(StatusCode.Unknown, "some error text from the server"));
        }

        await Assert.ThrowsAsync<CouchbaseException>(async ()=> await retryMock.RetryAsync(GrpcCall, request));
    }

}
#endif
