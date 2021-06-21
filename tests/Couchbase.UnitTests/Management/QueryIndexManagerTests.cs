using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Management.Query;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Management
{
    public class QueryIndexManagerTests
    {
        [Fact]
        public async Task Test_GetAllIndexesAsync()
        {
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\Management\query-index-partition-response.json");

            var buffer = new byte[response.Length];
            response.Read(buffer, 0, buffer.Length);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(buffer)
            });

            var httpClient = new CouchbaseHttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var serializer = new DefaultSerializer();
            var client = new QueryClient(httpClient, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, NoopMeter.Instance);

            var manager = new QueryIndexManager(client, new Mock<ILogger<QueryIndexManager>>().Object,
                new Redactor(new ClusterOptions()));

            var result =  await manager.GetAllIndexesAsync(It.IsAny<string>());

            var queryIndices = result as QueryIndex[] ?? result.ToArray();
            var rowWithPartition = queryIndices.FirstOrDefault(x => x.Partition == "HASH(`_type`)");
            Assert.NotNull(rowWithPartition);

            var rowWithCondition = queryIndices.FirstOrDefault(x => x.Condition == "(`_type` = \"User\")");
            Assert.NotNull(rowWithCondition);

            var rowWithIndexKey = queryIndices.FirstOrDefault(x=>x.IndexKey.Contains("`airportname`"));
            Assert.Equal("`airportname`", rowWithIndexKey.IndexKey.First());
        }
    }
}
