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
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Diagnostics.Metrics;
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
        [InlineData("query-timeout-response-200.json", HttpStatusCode.OK, typeof(AmbiguousTimeoutException))]
        [InlineData("query-unsupported-error-405.json", HttpStatusCode.MethodNotAllowed, typeof(PreparedStatementException))]
        public async Task Test(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
#if NET8_0_OR_GREATER
            await using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\" + file);
#else
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\" + file);
#endif

            var buffer = new byte[response.Length];
            await response.ReadExactlyAsync(buffer, 0, buffer.Length);

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
                .Setup(m => m.GetRandomQueryNode(It.IsAny<System.Collections.Generic.IList<System.Uri>>()))
                .Returns(nodeMock.Object);

            var serializer = DefaultSerializer.Instance;

            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);

            try
            {
                await client.QueryAsync<DynamicAttribute>("SELECT * FROM `default`", new QueryOptions());
            }
            catch (Exception e)
            {
                Assert.Equal(errorType, e.GetType());
            }
        }

        [Theory]
        [InlineData("query-badrequest-error-response-400.json", HttpStatusCode.BadRequest, typeof(PlanningFailureException))]
        [InlineData("query-n1ql-error-response-400.json", HttpStatusCode.BadRequest, typeof(PlanningFailureException))]
        [InlineData("query-notfound-response-404.json", HttpStatusCode.NotFound, typeof(PreparedStatementException))]
        [InlineData("query-service-error-response-503.json", HttpStatusCode.ServiceUnavailable, typeof(InternalServerFailureException))]
        [InlineData("query-timeout-response-200.json", HttpStatusCode.OK, typeof(AmbiguousTimeoutException))]
        [InlineData("query-unsupported-error-405.json", HttpStatusCode.MethodNotAllowed, typeof(PreparedStatementException))]
        public async Task Test_SystemTextJson(string file, HttpStatusCode httpStatusCode, Type errorType)
        {
#if NET8_0_OR_GREATER
            await using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\" + file);
#else
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\" + file);
#endif

            var buffer = new byte[response.Length];
            await response.ReadExactlyAsync(buffer, 0, buffer.Length);

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
                .Setup(m => m.GetRandomQueryNode(It.IsAny<System.Collections.Generic.IList<System.Uri>>()))
                .Returns(nodeMock.Object);

            // Do not use JsonPropertyNaming.CamelCase here to confirm that non-standard
            // options still deserialize errors correctly.
            var serializer = SystemTextJsonSerializer.Create(new JsonSerializerOptions());

            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);

            try
            {
                await client.QueryAsync<DynamicAttribute>("SELECT * FROM `default`", new QueryOptions());
            }
            catch (Exception e)
            {
                Assert.Equal(errorType, e.GetType());
            }
        }

        [Theory]
        [InlineData(typeof(DefaultSerializer))]
        [InlineData(typeof(NonStreamingSerializer))]
        public async Task TestSuccess(Type serializerType)
        {
#if NET8_0_OR_GREATER
            await using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");
#else
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");
#endif

            var buffer = new byte[response.Length];
            await response.ReadExactlyAsync(buffer, 0, buffer.Length);

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
                .Setup(m => m.GetRandomQueryNode(It.IsAny<System.Collections.Generic.IList<System.Uri>>()))
                .Returns(nodeMock.Object);

            var serializer = (ITypeSerializer) Activator.CreateInstance(serializerType);
            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, serializer,
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);

            var result = await client.QueryAsync<dynamic>("SELECT * FROM `default`", new QueryOptions());

            Assert.Equal(10, await result.CountAsync());
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
                .Setup(m => m.GetRandomQueryNode(It.IsAny<System.Collections.Generic.IList<System.Uri>>()))
                .Returns(nodeMock.Object);

            var primarySerializer = new Mock<ITypeSerializer> {DefaultValue = DefaultValue.Mock};
            var overrideSerializer = new Mock<ITypeSerializer> {DefaultValue = DefaultValue.Mock};

            var client = new QueryClient(httpClientFactory, mockServiceUriProvider.Object, primarySerializer.Object,
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);

            await client.QueryAsync<object>("SELECT * FROM `default`",
                new QueryOptions
                {
                    Serializer = overrideSerializer.Object
                });

            primarySerializer.Verify(
                m => m.DeserializeAsync<BlockQueryResult<object>.QueryResultData>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
                Times.Never);
            overrideSerializer.Verify(
                m => m.DeserializeAsync<BlockQueryResult<object>.QueryResultData>(It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }

        // name -> (options factory, whether positional or named parameters are in use)
        private static readonly Dictionary<string, (Func<QueryOptions> Create, bool HasParameters)> StatementOptions = new()
        {
            ["no parameters"] = (() => new QueryOptions(), false),
            ["positional parameter"] = (() => new QueryOptions().Parameter(1), true),
            ["positional parameters"] = (() => new QueryOptions().Parameter(1, "a"), true),
            ["named parameter"] = (() => new QueryOptions().Parameter("name", "a"), true),
            ["named parameters"] = (() => new QueryOptions().Parameter(
                new KeyValuePair<string, object>("a", 1), new KeyValuePair<string, object>("b", 2)), true),
            ["raw option only"] = (() => new QueryOptions().Raw("$name", "a"), false),
        };

        public static TheoryData<string, string, bool, bool> StatementAttributeCases()
        {
            // convention ("unwrapped" = a span with no semantic convention mapping), options name,
            // expect db.statement, expect db.query.text
            var data = new TheoryData<string, string, bool, bool>();
            foreach (var name in StatementOptions.Keys)
            {
                var hasParameters = StatementOptions[name].HasParameters;
                data.Add("unwrapped", name, true, false);
                data.Add(nameof(ObservabilitySemanticConvention.Legacy), name, true, false);
                data.Add(nameof(ObservabilitySemanticConvention.Modern), name, false, hasParameters);
                data.Add(nameof(ObservabilitySemanticConvention.Both), name, true, hasParameters);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(StatementAttributeCases))]
        public async Task QueryAsync_StatementAttribute_StableNameOnlyWithParameters(
            string convention, string optionsName, bool expectLegacy, bool expectStable)
        {
            const string statement = "SELECT * FROM `default` WHERE secret = 'value'";

            var innerSpan = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
            innerSpan.Setup(s => s.CanWrite).Returns(true);
            var span = convention == "unwrapped"
                ? innerSpan.Object
                : new RequestSpanWrapper(innerSpan.Object, null,
                    (ObservabilitySemanticConvention)Enum.Parse(typeof(ObservabilitySemanticConvention), convention));
            var tracer = new Mock<IRequestTracer>();
            tracer.Setup(t => t.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>()))
                .Returns(span);

            var client = CreateClientWithEmptyResponse(tracer.Object, out var serializer);
            var options = StatementOptions[optionsName].Create();
            options.Serializer = serializer;

            await client.QueryAsync<object>(statement, options);

            innerSpan.Verify(s => s.SetAttribute("db.statement", statement),
                expectLegacy ? Times.Once() : Times.Never());
            innerSpan.Verify(s => s.SetAttribute("db.query.text", statement),
                expectStable ? Times.Once() : Times.Never());
        }

        [Fact]
        public async Task QueryAsync_DispatchSpan_HasPeerAddressAndPort()
        {
            var rootSpan = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
            rootSpan.Setup(s => s.CanWrite).Returns(true);
            var childSpans = new List<Mock<IRequestSpan>>();
            rootSpan.Setup(s => s.ChildSpan(It.IsAny<string>())).Returns(() =>
            {
                var child = new Mock<IRequestSpan> { DefaultValue = DefaultValue.Mock };
                child.Setup(c => c.CanWrite).Returns(true);
                childSpans.Add(child);
                return child.Object;
            });
            var tracer = new Mock<IRequestTracer>();
            tracer.Setup(t => t.RequestSpan(It.IsAny<string>(), It.IsAny<IRequestSpan>()))
                .Returns(rootSpan.Object);

            var client = CreateClientWithEmptyResponse(tracer.Object, out var serializer);

            await client.QueryAsync<object>("SELECT 1", new QueryOptions { Serializer = serializer });

            rootSpan.Verify(s => s.ChildSpan(InnerRequestSpans.DispatchSpan.Name), Times.Once());
            var dispatch = childSpans.Single(c => c.Invocations.Any(i =>
                i.Arguments.Count == 2 && Equals(i.Arguments[0], InnerRequestSpans.DispatchSpan.Attributes.OperationId)));
            dispatch.Verify(s => s.SetAttribute("network.peer.address", "localhost"), Times.Once());
            dispatch.Verify(s => s.SetAttribute("network.peer.port", "8093"), Times.Once());
            rootSpan.Verify(s => s.SetAttribute("network.peer.address", It.IsAny<string>()), Times.Never());
        }

        private static QueryClient CreateClientWithEmptyResponse(IRequestTracer tracer, out ITypeSerializer serializer)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()).ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(Array.Empty<byte>())
            });

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.QueryUri).Returns(new Uri("http://localhost:8093"));
            var nodeAdapterMock = new Mock<NodeAdapter>();
            nodeAdapterMock.Object.CanonicalHostname = "localhost";
            nodeMock.Setup(n => n.NodesAdapter).Returns(nodeAdapterMock.Object);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryUri())
                .Returns(new Uri("http://localhost:8093"));
            mockServiceUriProvider
                .Setup(m => m.GetRandomQueryNode(It.IsAny<IList<Uri>>()))
                .Returns(nodeMock.Object);

            serializer = new Mock<ITypeSerializer> { DefaultValue = DefaultValue.Mock }.Object;

            return new QueryClient(new MockHttpClientFactory(httpClient), mockServiceUriProvider.Object, serializer,
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, tracer, TestRedactor.None);
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
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);

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
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);
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
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);
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
