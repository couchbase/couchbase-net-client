using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.IO.Serializers;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Analytics
{
    public class AnalyticsClientTests
    {
        [Theory]
        [InlineData(typeof(DefaultSerializer))]
        [InlineData(typeof(NonStreamingSerializer))]
        public async Task TestSuccess(Type serializerType)
        {
#if NET8_0_OR_GREATER
            await using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#else
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");
#endif

            var buffer = new byte[response.Length];
#if NET8_0_OR_GREATER
            await response.ReadExactlyAsync(buffer, 0, buffer.Length);
#else
            response.Read(buffer, 0, buffer.Length);
#endif

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
            nodeMock.Setup(n => n.AnalyticsUri)
                .Returns(new Uri("http://localhost:8096"));

            var nodeAdapterMock = new Mock<NodeAdapter>
            {
                Object =
                {
                    CanonicalHostname = "localhost"
                }
            };

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsUri())
                .Returns(new Uri("http://localhost:8096"));
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsNode())
                .Returns(nodeMock.Object);

            var serializer = (ITypeSerializer) Activator.CreateInstance(serializerType);
            var client = new AnalyticsClient(httpClientFactory, mockServiceUriProvider.Object, serializer ?? throw new InvalidOperationException(),
                new Mock<ILogger<AnalyticsClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);

            var result = await client.QueryAsync<dynamic>("SELECT * FROM `default`", new AnalyticsOptions());

            Assert.Equal(5, await result.CountAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Client_sets_AnalyticsPriority_Header(bool priority)
        {
            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request =>
                {
                    if (priority)
                    {
                        Assert.True(request.Headers.TryGetValues(AnalyticsClient.AnalyticsPriorityHeaderName,
                            out var values));
                        Assert.Equal("-1", values.First());
                    }
                    else
                    {
                        Assert.False(request.Headers.TryGetValues(AnalyticsClient.AnalyticsPriorityHeaderName, out _));
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK) {Content = new StringContent("{}")};
                })
            );
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.AnalyticsUri)
                .Returns(new Uri("http://localhost:8096"));

            var nodeAdapterMock = new Mock<NodeAdapter>
            {
                Object =
                {
                    CanonicalHostname = "localhost"
                }
            };

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsUri())
                .Returns(new Uri("http://localhost:8096"));
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsNode())
                .Returns(nodeMock.Object);

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);

            await client.QueryAsync<dynamic>("SELECT * FROM `default`;", new AnalyticsOptions().Priority(priority));
        }

        [Fact]
        public async Task QueryAsync_Sets_LastActivity()
        {
            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })
            );
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.AnalyticsUri)
                .Returns(new Uri("http://localhost:8096"));

            var nodeAdapterMock = new Mock<NodeAdapter>
            {
                Object =
                {
                    CanonicalHostname = "localhost"
                }
            };

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsUri())
                .Returns(new Uri("http://localhost:8096"));
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsNode())
                .Returns(nodeMock.Object);

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);

            Assert.Null(client.LastActivity);
            await client.QueryAsync<dynamic>("SELECT * FROM `default`;", new AnalyticsOptions());
            Assert.NotNull(client.LastActivity);
        }

        [Theory]
        [InlineData(24006, typeof(LinkNotFoundException))]
        [InlineData(24039, typeof(DataverseExistsException))]
        [InlineData(24040, typeof(DatasetExistsException))]
        [InlineData(24034, typeof(DataverseNotFoundException))]
        [InlineData(24044, typeof(DatasetNotFoundException))]
        [InlineData(24045, typeof(DatasetNotFoundException))]
        [InlineData(24025, typeof(DatasetNotFoundException))]
        [InlineData(23007, typeof(JobQueueFullException))]
        [InlineData(25000, typeof(InternalServerFailureException))]
        [InlineData(20000, typeof(AuthenticationFailureException))]
        [InlineData(23000, typeof(TemporaryFailureException))]
        [InlineData(24000, typeof(ParsingFailureException))]
        [InlineData(24047, typeof(IndexNotFoundException))]
        [InlineData(24048, typeof(IndexExistsException))]//24044, 24045, 24025
        [InlineData(24500, typeof(CompilationFailureException))]
        public async Task ShouldThrowException(int errorCode, Type type)
        {
            var response = "{\"requestID\":\"eb8a8d08-9e25-4473-81f8-6565c51a43d9\",\"signature\":{\"*\": \"*\"},\"errors\":[{\"code\":XXXX,\"msg\":\"Some error\"}],\"status\": \"fatal\"}";
            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(response.Replace("XXXX", errorCode.ToString()))
                })) ;
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.AnalyticsUri)
                .Returns(new Uri("http://localhost:8096"));

            var nodeAdapterMock = new Mock<NodeAdapter>
            {
                Object =
                {
                    CanonicalHostname = "localhost"
                }
            };

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsUri())
                .Returns(new Uri("http://localhost:8096"));
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsNode())
                .Returns(nodeMock.Object);

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);

            try
            {
                await client.QueryAsync<dynamic>("SELECT * FROM `default`;", new AnalyticsOptions());
            }
            catch (Exception ex)
            {
                if(ex.GetType() != type)
                {
                    throw;
                }
            }
        }

        [Fact]
        public async Task QueryAsync_Throws_CouchbaseException_If_EnterpriseAnalytics()
        {
            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })
            );
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.AnalyticsUri)
                .Returns(new Uri("http://localhost:8096"));
            nodeMock.Setup(n => n.HasAnalytics)
                .Returns(true);

            var nodeAdapterMock = new Mock<NodeAdapter>
            {
                Object =
                {
                    CanonicalHostname = "localhost"
                }
            };

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var globalConfig = ResourceHelper.ReadResource(@"Documents\Configs\config-with-analytics-prod.json", InternalSerializationContext.Default.BucketConfig);

            var clusterContext = new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"))
            {
                GlobalConfig = globalConfig
            };

            clusterContext.AddNode(nodeMock.Object);

            var serviceUriProvider = new ServiceUriProvider(clusterContext);

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClientFactory, serviceUriProvider, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);

            var exception = await Assert.ThrowsAsync<CouchbaseException>(
                () => client.QueryAsync<dynamic>("SELECT * FROM `default`;", new AnalyticsOptions())
            );

            Assert.Equal("This SDK is for Couchbase Server (operational) clusters, but the remote cluster is an Enterprise Analytics cluster. " +
                         "Please use the Enterprise Analytics SDK to access this cluster", exception.Message);
        }

        [Fact]
        public async Task QueryAsync_Succeeds_When_Prod_Is_Valid()
        {
            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(_ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })
            );
            var httpClientFactory = new MockHttpClientFactory(httpClient);

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.AnalyticsUri)
                .Returns(new Uri("http://localhost:8096"));
            nodeMock.Setup(n => n.HasAnalytics)
                .Returns(true);

            var nodeAdapterMock = new Mock<NodeAdapter>
            {
                Object =
                {
                    CanonicalHostname = "localhost"
                }
            };

            nodeMock.Setup(n => n.NodesAdapter)
                .Returns(nodeAdapterMock.Object);

            var globalConfig = ResourceHelper.ReadResource(@"Documents\Configs\config-with-server-prod.json", InternalSerializationContext.Default.BucketConfig);

            var clusterContext = new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"))
            {
                GlobalConfig = globalConfig
            };

            clusterContext.AddNode(nodeMock.Object);

            var serviceUriProvider = new ServiceUriProvider(clusterContext);

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClientFactory, serviceUriProvider, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object, NoopRequestTracer.Instance, new Mock<IAppTelemetryCollector>().Object);


            var noException = await Record.ExceptionAsync(
                () => client.QueryAsync<dynamic>("SELECT * FROM `default`;", new AnalyticsOptions())
            );
            Assert.Null(noException);
        }
    }
}
