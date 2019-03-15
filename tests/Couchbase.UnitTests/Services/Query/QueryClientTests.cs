using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.Serializers;
using Couchbase.Services.Query;
using Couchbase.UnitTests.Utils;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Services.Query
{
    public class QueryClientTests
    {
        [Theory]
        [InlineData("query-badrequest-error-response-400.json", HttpStatusCode.BadRequest, typeof(QueryException))]
        [InlineData("query-n1ql-error-response-400.json", HttpStatusCode.BadRequest, typeof(QueryException))]
        [InlineData("query-notfound-response-404.json", HttpStatusCode.NotFound, typeof(QueryException))]
        [InlineData("query-service-error-response-503.json", HttpStatusCode.ServiceUnavailable, typeof(QueryException))]
        [InlineData("query-timeout-response-200.json", HttpStatusCode.OK, typeof(QueryException))]
        [InlineData("query-unsupported-error-405.json", HttpStatusCode.MethodNotAllowed, typeof(QueryException))]
        public async Task Test(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
            using (var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\" + file))
            {
                var buffer = new byte[response.Length];
                response.Read(buffer, 0, buffer.Length);

                var handlerMock = new Mock<HttpMessageHandler>();
                handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode =  httpStatusCode,
                    Content = new ByteArrayContent(buffer)
                });

                var httpClient = new HttpClient(handlerMock.Object)
                {
                    BaseAddress = new Uri("http://localhost:8091")
                };
                var config = new Configuration().WithBucket("default").WithServers("http://localhost:8901");
                var client = new QueryClient(httpClient, new JsonDataMapper(new DefaultSerializer()), config);

                try
                {
                    await client.QueryAsync<DynamicAttribute>("SELECT * FROM `default`", new QueryOptions());
                }
                catch (Exception e)
                {
                    Assert.True(e.GetType() == errorType);
                }
            }
        }
    }
}
