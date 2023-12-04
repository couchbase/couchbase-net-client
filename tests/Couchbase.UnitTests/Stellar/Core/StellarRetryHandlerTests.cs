#if NETCOREAPP3_1_OR_GREATER
using Couchbase.Protostellar.KV.V1;
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
    [InlineData( "document", "DocumentNotFound", StatusCode.NotFound, "\n\bdocument\u0012\"default/_default/_default/fake_doc")]
    [InlineData( "collection", "CollectionExists", StatusCode.AlreadyExists, "\n\ncollection\u0012\u0017default/_default/5efcb8")]
    [InlineData( "queryindex", "IndexExists", StatusCode.AlreadyExists, "\n\nqueryindex\u0012\b#primary")]
    public async void RetryThrowsTypeUrlResourceErrors(string resourceType, string expectedException, StatusCode statusCode, string detailValue)
    {
        var any = new Any
        {
            Value = ByteString.CopyFromUtf8(detailValue),
            TypeUrl = StellarRetryStrings.TypeUrlResourceInfo
        };
        var retryMock = new Mock<StellarRetryHandler>();
        retryMock.Setup(handler => handler.StatusDeserializer(It.IsAny<RpcException>())).Returns(any);

        var grpcCall = async () =>
        {
            var thrower = () => { throw new RpcException(new Status(statusCode, "NoDetail")); };
            thrower.Invoke();
            return new GetResponse();
        };

        var result = await Record.ExceptionAsync( () => retryMock.Object.RetryAsync(grpcCall, new StellarRequest())).ConfigureAwait(false);
        Assert.Contains(expectedException, result!.ToString());
    }
}
#endif
