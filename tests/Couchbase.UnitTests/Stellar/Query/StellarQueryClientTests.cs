#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Protostellar.View.V1;
using Couchbase.Query;
using Couchbase.Stellar.Core.Retry;
using Couchbase.Stellar.Query;
using Couchbase.UnitTests.Stellar.Utils;
using Couchbase.UnitTests.Utils;
using Grpc.Core;
using Grpc.Net.Client;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Query;

public class StellarQueryClientTests
{
    [Fact]
    public async Task Test_QueryAsync()
    {
        var cluster = StellarMocks.CreateClusterFromMocks();
        var queryServiceClient = new Mock<QueryService.QueryServiceClient>().Object;
        var retryHandler = new StellarRetryHandler();
        var typeSerializer = SystemTextJsonSerializer.Create();

        var queryClient = new StellarQueryClient(cluster, queryServiceClient, typeSerializer, retryHandler);
        var result = await queryClient.QueryAsync<dynamic>("SELECT 1;", new QueryOptions());

        await Assert.ThrowsAsync<NullReferenceException>(async()=> await result.GetAsyncEnumerator().MoveNextAsync());
    }
}
#endif
