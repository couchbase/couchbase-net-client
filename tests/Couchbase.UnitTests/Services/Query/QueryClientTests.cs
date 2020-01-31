using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Services.Query
{
    public class QueryClientTests
    {
        [Theory]
        [InlineData("query-badrequest-error-response-400.json", HttpStatusCode.BadRequest, typeof(PlanningFailureException))]
        [InlineData("query-n1ql-error-response-400.json", HttpStatusCode.BadRequest, typeof(PlanningFailureException))]
        [InlineData("query-notfound-response-404.json", HttpStatusCode.NotFound, typeof(PreparedStatementException))]
        [InlineData("query-service-error-response-503.json", HttpStatusCode.ServiceUnavailable, typeof(InternalServerFailureException))]
        [InlineData("query-timeout-response-200.json", HttpStatusCode.OK, typeof(UnambiguousTimeoutException))]
        [InlineData("query-unsupported-error-405.json", HttpStatusCode.MethodNotAllowed, typeof(PreparedStatementException))]
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
                    StatusCode = httpStatusCode,
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
                    new Mock<ILogger<QueryClient>>().Object);

                try
                {
                    await client.QueryAsync<DynamicAttribute>("SELECT * FROM `default`", new QueryOptions());
                }
                catch (Exception e)
                {
                    Assert.Equal(errorType, e.GetType());
                }
            }
        }

        [Theory]
        [InlineData(typeof(DefaultSerializer))]
        [InlineData(typeof(NonStreamingSerializer))]
        public async Task TestSuccess(Type serializerType)
        {
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

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

            var serializer = (ITypeSerializer) Activator.CreateInstance(serializerType);
            var client = new QueryClient(httpClient, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<QueryClient>>().Object);

            var result = await client.QueryAsync<dynamic>("SELECT * FROM `default`", new QueryOptions());

            Assert.Equal(10, await result.CountAsync());
        }

        [Fact]
        public void EnhancedPreparedStatements_defaults_to_false()
        {
            var httpClient = new CouchbaseHttpClient(new HttpClientHandler())
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var client = new QueryClient(httpClient, mockServiceUriProvider.Object, new DefaultSerializer(),
                new Mock<ILogger<QueryClient>>().Object);

            Assert.False(client.EnhancedPreparedStatementsEnabled);
        }

        [Fact]
        public void EnhancedPreparedStatements_is_set_to_true_if_enabled_in_cluster_caps()
        {
            var httpClient = new CouchbaseHttpClient(new HttpClientHandler())
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var client = new QueryClient(httpClient, mockServiceUriProvider.Object, new DefaultSerializer(),
                new Mock<ILogger<QueryClient>>().Object);
            Assert.False(client.EnhancedPreparedStatementsEnabled);

            var clusterCapabilities = new ClusterCapabilities
            {
                Capabilities = new Dictionary<string, IEnumerable<string>>
                {
                    {
                        ServiceType.Query.GetDescription(),
                        new List<string> {ClusterCapabilityFeatures.EnhancedPreparedStatements.GetDescription()}
                    }
                }
            };

            client.UpdateClusterCapabilities(clusterCapabilities);
            Assert.True(client.EnhancedPreparedStatementsEnabled);
        }
    }
}
