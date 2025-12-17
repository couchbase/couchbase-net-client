using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
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

namespace Couchbase.UnitTests.Query
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

                var httpClient = new HttpClient(handlerMock.Object)
                {
                    BaseAddress = new Uri("http://localhost:8091")
                };
                var httpClientFactory = new MockHttpClientFactory(httpClient);

                var nodeMock = new Mock<IClusterNode>();
                nodeMock
                    .Setup(n => n.QueryUri)
                    .Returns(new Uri("http://localhost:8093"));

                var nodeAdapterMock = new Mock<NodeAdapter>();
                nodeAdapterMock.Object.CanonicalHostname = "localhost";

                nodeMock.Setup(n => n.NodesAdapter)
                    .Returns(nodeAdapterMock.Object);

                var mockServiceUriProvider = new Mock<IServiceUriProvider>();
                mockServiceUriProvider
                    .Setup(m => m.GetRandomQueryUri())
                    .Returns(new Uri("http://localhost:8093"));
                mockServiceUriProvider
                    .Setup(m => m.GetRandomQueryNode())
                    .Returns(nodeMock.Object);

                var serializer = DefaultSerializer.Instance;

                var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                    NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);

                try
                {
                    await client.QueryAsync<DynamicAttribute>("SELECT * FROM `default`", new QueryOptions()).ConfigureAwait(true);
                }
                catch (Exception e)
                {
                    Assert.Equal(errorType, e.GetType());
                }
            }
        }

        [Theory]
        [InlineData("query-badrequest-error-response-400.json", HttpStatusCode.BadRequest, typeof(PlanningFailureException))]
        [InlineData("query-n1ql-error-response-400.json", HttpStatusCode.BadRequest, typeof(PlanningFailureException))]
        [InlineData("query-notfound-response-404.json", HttpStatusCode.NotFound, typeof(PreparedStatementException))]
        [InlineData("query-service-error-response-503.json", HttpStatusCode.ServiceUnavailable, typeof(InternalServerFailureException))]
        [InlineData("query-timeout-response-200.json", HttpStatusCode.OK, typeof(UnambiguousTimeoutException))]
        [InlineData("query-unsupported-error-405.json", HttpStatusCode.MethodNotAllowed, typeof(PreparedStatementException))]
        public async Task Test_SystemTextJson(string file, HttpStatusCode httpStatusCode, Type errorType)
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

                var httpClient = new HttpClient(handlerMock.Object)
                {
                    BaseAddress = new Uri("http://localhost:8091")
                };
                var httpClientFactory = new MockHttpClientFactory(httpClient);

                var nodeMock = new Mock<IClusterNode>();
                nodeMock
                    .Setup(n => n.QueryUri)
                    .Returns(new Uri("http://localhost:8093"));

                var nodeAdapterMock = new Mock<NodeAdapter>();
                nodeAdapterMock.Object.CanonicalHostname = "localhost";

                nodeMock.Setup(n => n.NodesAdapter)
                    .Returns(nodeAdapterMock.Object);

                var mockServiceUriProvider = new Mock<IServiceUriProvider>();
                mockServiceUriProvider
                    .Setup(m => m.GetRandomQueryNode())
                    .Returns(nodeMock.Object);

                // Do not use JsonPropertyNaming.CamelCase here to confirm that non-standard
                // options still deserialize errors correctly.
                var serializer = SystemTextJsonSerializer.Create(new JsonSerializerOptions());

                var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                    NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);

                try
                {
                    await client.QueryAsync<DynamicAttribute>("SELECT * FROM `default`", new QueryOptions()).ConfigureAwait(true);
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

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var nodeMock = new Mock<IClusterNode>();
            nodeMock
                .Setup(n => n.QueryUri)
                .Returns(new Uri("http://localhost:8093"));

            var nodeAdapterMock = new Mock<NodeAdapter>();
            nodeAdapterMock.Object.CanonicalHostname = "localhost";

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryNode())
                .Returns(nodeMock.Object);

            var serializer = (ITypeSerializer) Activator.CreateInstance(serializerType);
            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, new Mock<AppTelemetryCollector>().Object);

            var result = await client.QueryAsync<dynamic>("SELECT * FROM `default`", new QueryOptions()).ConfigureAwait(true);

            Assert.Equal(10, await result.CountAsync().ConfigureAwait(true));
        }

        [Fact]
        public async Task QueryAsync_SerializerOverride_UsesOverride()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(Array.Empty<byte>())
            });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var nodeMock = new Mock<IClusterNode>();
            nodeMock
                .Setup(n => n.QueryUri)
                .Returns(new Uri("http://localhost:8093"));

            var nodeAdapterMock = new Mock<NodeAdapter>();
            nodeAdapterMock.Object.CanonicalHostname = "localhost";

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryNode())
                .Returns(nodeMock.Object);

            var primarySerializer = new Mock<ITypeSerializer> {DefaultValue = DefaultValue.Mock};
            var overrideSerializer = new Mock<ITypeSerializer> {DefaultValue = DefaultValue.Mock};

            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, primarySerializer.Object,
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, new Mock<AppTelemetryCollector>().Object);

            await client.QueryAsync<object>("SELECT * FROM `default`",
                new QueryOptions
                {
                    Serializer = overrideSerializer.Object
                }).ConfigureAwait(true);

            primarySerializer.Verify(
                m => m.DeserializeAsync<BlockQueryResult<object>.QueryResultData>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
                Times.Never);
            overrideSerializer.Verify(
                m => m.DeserializeAsync<BlockQueryResult<object>.QueryResultData>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public void EnhancedPreparedStatements_defaults_to_false()
        {
            var httpClient = new HttpClient(new HttpClientHandler())
            {
                BaseAddress = new Uri("http://localhost:8091")
            };
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, new DefaultSerializer(),
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, new Mock<AppTelemetryCollector>().Object);

            Assert.False(client.EnhancedPreparedStatementsEnabled);
        }

        [Fact]
        public void EnhancedPreparedStatements_is_set_to_true_if_enabled_in_cluster_caps()
        {
            var httpClient = new HttpClient(new HttpClientHandler())
            {
                BaseAddress = new Uri("http://localhost:8091")
            };
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, new DefaultSerializer(),
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, new Mock<AppTelemetryCollector>().Object);
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

        [Fact]
        public void UseReplicaEnabled_is_set_to_true_if_enabled_in_cluster_caps()
        {
            var httpClient = new HttpClient(new HttpClientHandler())
            {
                BaseAddress = new Uri("http://localhost:8091")
            };
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));

            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, new DefaultSerializer(),
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, new Mock<AppTelemetryCollector>().Object);
            Assert.False(client.UseReplicaEnabled);

            var clusterCapabilities = new ClusterCapabilities
            {
                Capabilities = new Dictionary<string, IEnumerable<string>>
                {
                    {
                        ServiceType.Query.GetDescription(),
                        new List<string> {ClusterCapabilityFeatures.UseReplicaFeature.GetDescription()}
                    }
                }
            };

            client.UpdateClusterCapabilities(clusterCapabilities);
            Assert.True(client.UseReplicaEnabled);
        }
    }
}
