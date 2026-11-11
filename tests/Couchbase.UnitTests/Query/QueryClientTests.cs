using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Retry;
using Couchbase.Core.Retry.Query;
using Couchbase.Query;
using Couchbase.Query.Conversational;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
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

        [Fact]
        public async Task UseReplicaEnabled_is_set_when_a_newer_config_adds_readFromReplica()
        {
            var httpClientFactory = new MockHttpClientFactory(new HttpClient(new HttpClientHandler()));
            var client = new QueryClient(httpClientFactory, new Mock<IServiceUriProvider>().Object, new DefaultSerializer(),
                NullFallbackTypeSerializerProvider.Instance, new Mock<ILogger<QueryClient>>().Object, NoopRequestTracer.Instance, TestRedactor.None);
            using var cluster = new Cluster(new ClusterOptions()
                .WithConnectionString("couchbase://localhost")
                .WithPasswordAuthentication("username", "password"));
            cluster.LazyQueryClient = LazyServiceShimFactory.Create<IQueryClient>(client);

            cluster.Context.UpdateClusterCapsConfig(CreateConfig(1, "enhancedPreparedStatements"));
            // A change before bootstrap does not create the client.
            Assert.False(cluster.LazyQueryClient.IsValueCreated);

            cluster.UpdateClusterCapabilities();
            Assert.True(client.EnhancedPreparedStatementsEnabled);
            Assert.False(client.UseReplicaEnabled);

            await cluster.Context.ConfigUpdatedAsync(CreateConfig(2, "enhancedPreparedStatements", "readFromReplica"));
            Assert.True(client.UseReplicaEnabled);

            static BucketConfig CreateConfig(ulong rev, params string[] queryCaps)
            {
                var config = new BucketConfig
                {
                    Name = "default",
                    Rev = rev,
                    ClusterCapabilities = new Dictionary<string, IEnumerable<string>> { ["n1ql"] = queryCaps }
                };
                config.OnDeserialized();
                return config;
            }
        }

        #region Target node

        [Fact]
        public async Task QueryAsync_TargetNode_SendsToThatNodeOnly()
        {
            var target = QueryNode("uuid-a", "http://a:8093/query/service");
            var random = QueryNode("uuid-b", "http://b:8093/query/service");
            var provider = QueryNodeProvider(random, target);
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(provider, sent, SuccessResponse);

            var options = new QueryOptions
            {
                LastDispatchedNode = new Uri("http://c:8093/query/service"),
                Conversational = new ConversationalRequest(ConversationalOperation.Ask, "chat-1", "uuid-a")
            };
            var request = new QueryRequest
            {
                Options = options,
                ExcludedNodes = new List<Uri> { new("http://a:8093/query/service") }
            };

            using var result = await client.QueryAsync<dynamic>("USING AI hello", options, request);

            var (uri, body) = Assert.Single(sent);
            Assert.Equal(new Uri("http://a:8093/query/service"), uri);
            using var json = JsonDocument.Parse(body);
            Assert.Equal("chat-1", json.RootElement.GetProperty("natural_chatid").GetString());
            provider.Verify(m => m.GetRandomQueryNode(It.IsAny<IList<Uri>>()), Times.Never);
            target.VerifyGet(n => n.NodesAdapter, Times.AtLeastOnce());
            random.VerifyGet(n => n.NodesAdapter, Times.Never());
        }

        [Theory]
        [InlineData(nameof(ConversationalOperation.Ask), typeof(ChatCurrentlyUnavailableException))]
        [InlineData(nameof(ConversationalOperation.Pause), typeof(ChatCurrentlyUnavailableException))]
        [InlineData(nameof(ConversationalOperation.End), typeof(ChatCurrentlyUnavailableException))]
        [InlineData(nameof(ConversationalOperation.BeginChat), typeof(ServiceNotAvailableException))]
        [InlineData(nameof(ConversationalOperation.Resume), typeof(ServiceNotAvailableException))]
        [InlineData(nameof(ConversationalOperation.Query), typeof(ServiceNotAvailableException))]
        public async Task QueryAsync_TargetNodeNotInClusterMap_ThrowsWithoutSendingOrRetrying(string operation, Type expected)
        {
            var provider = QueryNodeProvider(QueryNode("uuid-b", "http://b:8093/query/service"));
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(provider, sent, SuccessResponse);

            var op = (ConversationalOperation)Enum.Parse(typeof(ConversationalOperation), operation);
            var options = new QueryOptions
            {
                Conversational = new ConversationalRequest(op, "chat-1", "uuid-gone")
            };
            var request = new QueryRequest
            {
                Options = options,
                Statement = "USING AI hello",
                Timeout = TimeSpan.FromSeconds(30)
            };

            var calls = 0;
            var ex = await Assert.ThrowsAsync(expected, () =>
                CreateRetryOrchestrator().RetryAsync(() =>
                {
                    calls++;
                    return client.QueryAsync<dynamic>(request.Statement, options, request);
                }, request));

            if (ex is ChatCurrentlyUnavailableException chat)
            {
                Assert.Equal("chat-1", chat.ChatId);
                Assert.Equal("uuid-gone", chat.OwnerNodeUuid);
                Assert.Equal(operation.ToLowerInvariant(), chat.Operation);
                Assert.Null(chat.Context);
                Assert.Contains("uuid-gone", chat.Message);
                Assert.Contains("ResumeAsync", chat.Message);
            }

            Assert.Equal(1, calls);
            Assert.Equal(0u, request.Attempts);
            Assert.Empty(sent);
            provider.Verify(m => m.GetRandomQueryNode(It.IsAny<IList<Uri>>()), Times.Never);
        }

        [Fact]
        public async Task QueryAsync_TargetNodePreConnectFailure_RetriesOnTheSameNode()
        {
            var target = QueryNode("uuid-a", "http://a:8093/query/service");
            var provider = QueryNodeProvider(QueryNode("uuid-b", "http://b:8093/query/service"), target);
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(provider, sent, ConnectionRefused, SuccessResponse);

            var lastDispatchedNode = new Uri("http://c:8093/query/service");
            var options = new QueryOptions
            {
                LastDispatchedNode = lastDispatchedNode,
                Conversational = new ConversationalRequest(ConversationalOperation.Ask, "chat-1", "uuid-a")
            };
            var request = new QueryRequest
            {
                Options = options,
                Statement = "USING AI hello",
                Timeout = TimeSpan.FromSeconds(30)
            };

            using var result = await CreateRetryOrchestrator().RetryAsync(
                () => client.QueryAsync<dynamic>(request.Statement, options, request), request);

            var targetUri = new Uri("http://a:8093/query/service");
            Assert.Equal(new[] { targetUri, targetUri }, sent.Select(x => x.Uri));
            Assert.Equal(1u, request.Attempts);
            Assert.Null(request.ExcludedNodes);
            Assert.Equal(lastDispatchedNode, options.LastDispatchedNode);
            provider.Verify(m => m.GetQueryNode("uuid-a"), Times.Exactly(2));
            provider.Verify(m => m.GetRandomQueryNode(It.IsAny<IList<Uri>>()), Times.Never);
        }

        [Fact]
        public async Task QueryAsync_TargetNodeLeavesAfterPreConnectFailure_ThrowsChatCurrentlyUnavailable()
        {
            var target = QueryNode("uuid-a", "http://a:8093/query/service");
            var provider = QueryNodeProvider(QueryNode("uuid-b", "http://b:8093/query/service"));
            provider.SetupSequence(m => m.GetQueryNode("uuid-a"))
                .Returns(target.Object)
                .Returns((IClusterNode)null);
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(provider, sent, ConnectionRefused);

            var options = new QueryOptions
            {
                Conversational = new ConversationalRequest(ConversationalOperation.Ask, "chat-1", "uuid-a")
            };
            var request = new QueryRequest
            {
                Options = options,
                Statement = "USING AI hello",
                Timeout = TimeSpan.FromSeconds(30)
            };

            var ex = await Assert.ThrowsAsync<ChatCurrentlyUnavailableException>(() =>
                CreateRetryOrchestrator().RetryAsync(
                    () => client.QueryAsync<dynamic>(request.Statement, options, request), request));

            Assert.Equal("chat-1", ex.ChatId);
            Assert.Equal("uuid-a", ex.OwnerNodeUuid);
            Assert.Equal("ask", ex.Operation);
            Assert.Null(ex.Context);
            Assert.Equal(new Uri("http://a:8093/query/service"), Assert.Single(sent).Uri);
            Assert.Equal(1u, request.Attempts);
            provider.Verify(m => m.GetQueryNode("uuid-a"), Times.Exactly(2));
            provider.Verify(m => m.GetRandomQueryNode(It.IsAny<IList<Uri>>()), Times.Never);
        }

        [Fact]
        public async Task QueryAsync_NoTargetNodePreConnectFailure_ExcludesTheNode()
        {
            var random = QueryNode("uuid-b", "http://b:8093/query/service");
            var provider = QueryNodeProvider(random);
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(provider, sent, ConnectionRefused);

            var options = new QueryOptions { LastDispatchedNode = new Uri("http://c:8093/query/service") };
            var request = new QueryRequest { Options = options };

            using var result = await client.QueryAsync<dynamic>("SELECT 1", options, request);

            Assert.Equal(RetryReason.ServiceNotAvailable, result.RetryReason);
            Assert.Equal(new[] { new Uri("http://c:8093/query/service") }, request.ExcludedNodes);
            Assert.Null(options.LastDispatchedNode);
            provider.Verify(m => m.GetQueryNode(It.IsAny<string>()), Times.Never);
        }

        private static Mock<IClusterNode> QueryNode(string nodeUuid, string queryUri)
        {
            var uri = new Uri(queryUri);
            var node = new Mock<IClusterNode>();
            node.SetupGet(n => n.NodeUuid).Returns(nodeUuid);
            node.SetupGet(n => n.HasQuery).Returns(true);
            node.SetupGet(n => n.QueryUri).Returns(uri);
            node.SetupGet(n => n.NodesAdapter).Returns(new NodeAdapter { CanonicalHostname = uri.Host });
            return node;
        }

        private static Mock<IServiceUriProvider> QueryNodeProvider(Mock<IClusterNode> randomNode,
            params Mock<IClusterNode>[] nodesByUuid)
        {
            var provider = new Mock<IServiceUriProvider>();
            provider
                .Setup(m => m.GetRandomQueryNode(It.IsAny<IList<Uri>>()))
                .Returns(randomNode.Object);
            foreach (var node in nodesByUuid)
            {
                provider.Setup(m => m.GetQueryNode(node.Object.NodeUuid)).Returns(node.Object);
            }

            return provider;
        }

        private static QueryClient CreateQueryClient(Mock<IServiceUriProvider> provider,
            List<(Uri Uri, string Body)> sent, params Func<HttpResponseMessage>[] responses) =>
            CreateQueryClient(provider, sent, new Mock<ILogger<QueryClient>>().Object, responses);

        private static QueryClient CreateQueryClient(Mock<IServiceUriProvider> provider,
            List<(Uri Uri, string Body)> sent, ILogger<QueryClient> logger, params Func<HttpResponseMessage>[] responses) =>
            CreateQueryClient(provider, sent, logger, NoopRequestTracer.Instance, responses);

        private static QueryClient CreateQueryClient(Mock<IServiceUriProvider> provider,
            List<(Uri Uri, string Body)> sent, ILogger<QueryClient> logger, IRequestTracer tracer,
            params Func<HttpResponseMessage>[] responses)
        {
            var handler = new Mock<HttpMessageHandler>();
            handler.Protected().Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>(async (message, _) =>
                {
                    sent.Add((message.RequestUri, await ReadBodyAsync(message.Content)));
                    return responses[sent.Count - 1]();
                });

            // QueryClient disposes the HttpClient on a transport error, so each attempt needs a new one.
            var httpClientFactory = new MockHttpClientFactory(() => new HttpClient(handler.Object, disposeHandler: false));

            return new QueryClient(httpClientFactory, provider.Object, DefaultSerializer.Instance,
                NullFallbackTypeSerializerProvider.Instance, logger,
                tracer, TestRedactor.None);
        }

        private static RetryOrchestrator CreateRetryOrchestrator() =>
            new(new FakeTimeProvider(), new Mock<ILogger<RetryOrchestrator>>().Object, TestRedactor.None)
            {
                Delay = (_, _) => Task.CompletedTask
            };

        // net48 has no ReadAsStringAsync overload with a token, so the handler token stays out of scope.
        private static Task<string> ReadBodyAsync(HttpContent content) => content.ReadAsStringAsync();

        private static HttpResponseMessage SuccessResponse() =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"requestID":"r-1","status":"success","results":[]}""")
            };

        private static HttpResponseMessage ConnectionRefused() =>
            throw new HttpRequestException("Connection refused",
                new SocketException((int)SocketError.ConnectionRefused));

        #endregion

        #region Conversational retry and timeout

        private const string RetryableServerError =
            """{"requestID":"r-1","errors":[{"code":5010,"msg":"Try again","retry":true}],"status":"fatal"}""";

        [Theory]
        [InlineData(nameof(ConversationalOperation.Query), "http://b:8093/query/service")]
        [InlineData(nameof(ConversationalOperation.Ask), "http://a:8093/query/service")]
        public async Task QueryAsync_ConversationalRetryableServerError_IsRetried(string operation, string expectedUri)
        {
            var target = QueryNode("uuid-a", "http://a:8093/query/service");
            var random = QueryNode("uuid-b", "http://b:8093/query/service");
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(QueryNodeProvider(random, target), sent,
                JsonResponse(RetryableServerError, HttpStatusCode.ServiceUnavailable), SuccessResponse);

            var options = new QueryOptions { Conversational = Conversational(operation) };
            var request = new QueryRequest
            {
                Options = options,
                Statement = "USING AI hello",
                Timeout = TimeSpan.FromSeconds(30)
            };

            using var result = await CreateRetryOrchestrator().RetryAsync(
                () => client.QueryAsync<dynamic>(request.Statement, options, request), request);

            var uri = new Uri(expectedUri);
            Assert.Equal(new[] { uri, uri }, sent.Select(x => x.Uri));
            Assert.Equal(1u, request.Attempts);
        }

        [Fact]
        public async Task QueryAsync_OrdinaryRetryableServerError_IsRetried()
        {
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(QueryNodeProvider(QueryNode("uuid-a", "http://a:8093/query/service")), sent,
                JsonResponse(RetryableServerError, HttpStatusCode.ServiceUnavailable), SuccessResponse);

            var options = new QueryOptions();
            var request = new QueryRequest
            {
                Options = options,
                Statement = "SELECT 1",
                Timeout = TimeSpan.FromSeconds(30)
            };

            using var result = await CreateRetryOrchestrator().RetryAsync(
                () => client.QueryAsync<dynamic>(request.Statement, options, request), request);

            Assert.Equal(2, sent.Count);
            Assert.Equal(1u, request.Attempts);
        }

        [Theory]
        [InlineData(nameof(ConversationalOperation.BeginChat))]
        [InlineData(nameof(ConversationalOperation.Ask))]
        [InlineData(nameof(ConversationalOperation.Pause))]
        [InlineData(nameof(ConversationalOperation.Resume))]
        [InlineData(nameof(ConversationalOperation.End))]
        public async Task QueryAsync_ChatAmbiguousTransportError_IsNotRetried(string operation)
        {
            var node = QueryNode("uuid-a", "http://a:8093/query/service");
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(QueryNodeProvider(node, node), sent, ConnectionReset, SuccessResponse);

            var options = new QueryOptions { Conversational = Conversational(operation) };
            var request = new QueryRequest
            {
                Options = options,
                Statement = "USING AI hello",
                Timeout = TimeSpan.FromSeconds(30)
            };

            await Assert.ThrowsAsync<RequestCanceledException>(() =>
                CreateRetryOrchestrator().RetryAsync(
                    () => client.QueryAsync<dynamic>(request.Statement, options, request), request));

            Assert.Single(sent);
            Assert.Equal(0u, request.Attempts);
            Assert.Null(request.ExcludedNodes);
        }

        [Theory]
        [InlineData(nameof(ConnectionRefused))]
        [InlineData(nameof(ConnectionReset))]
        public async Task QueryAsync_OneShotConversationalTransportError_RetriesOnAnotherNode(string failure)
        {
            var a = QueryNode("uuid-a", "http://a:8093/query/service");
            var b = QueryNode("uuid-b", "http://b:8093/query/service");
            var provider = new Mock<IServiceUriProvider>();
            provider
                .Setup(m => m.GetRandomQueryNode(It.IsAny<IList<Uri>>()))
                .Returns<IList<Uri>>(excluded => excluded?.Contains(a.Object.QueryUri) == true ? b.Object : a.Object);
            var sent = new List<(Uri Uri, string Body)>();
            Func<HttpResponseMessage> transportError = failure == nameof(ConnectionRefused) ? ConnectionRefused : ConnectionReset;
            var client = CreateQueryClient(provider, sent, transportError, SuccessResponse);

            var options = new QueryOptions { Conversational = Conversational(nameof(ConversationalOperation.Query)) };
            var request = new QueryRequest
            {
                Options = options,
                Statement = "USING AI hello",
                Timeout = TimeSpan.FromSeconds(30)
            };

            using var result = await CreateRetryOrchestrator().RetryAsync(
                () => client.QueryAsync<dynamic>(request.Statement, options, request), request);

            Assert.Equal(new[] { a.Object.QueryUri, b.Object.QueryUri }, sent.Select(x => x.Uri));
            Assert.Equal(1u, request.Attempts);
        }

        [Theory]
        [InlineData(nameof(ConversationalOperation.Query), true)]
        [InlineData(nameof(ConversationalOperation.Ask), false)]
        [InlineData(nameof(ConversationalOperation.End), false)]
        public async Task RetryAsync_ReasonWithoutNonIdempotentRetries_RetriesOnlyTheOneShotQuery(string operation, bool retried)
        {
            var request = new QueryRequest
            {
                Options = new QueryOptions { Conversational = Conversational(operation) },
                Timeout = TimeSpan.FromSeconds(30)
            };
            var noRetry = new RequestCanceledException("The socket closed while the request was in flight.");

            var calls = 0;
            Task<IQueryResult<dynamic>> Send()
            {
                calls++;
                var result = new BlockQueryResult<dynamic>(Stream.Null, DefaultSerializer.Instance);
                if (calls == 1)
                {
                    result.RetryReason = RetryReason.SocketClosedWhileInFlight;
                    result.NoRetryException = noRetry;
                }

                return Task.FromResult<IQueryResult<dynamic>>(result);
            }

            var outcome = await Record.ExceptionAsync(() => CreateRetryOrchestrator().RetryAsync(Send, request));

            Assert.Same(retried ? null : noRetry, outcome);
            Assert.Equal(retried ? 2 : 1, calls);
        }

        [Theory]
        [InlineData(nameof(ConversationalOperation.Query), false, "token", typeof(UnambiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Query), false, "status timeout", typeof(UnambiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Query), false, "1080 fatal", typeof(UnambiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Query), false, "1080 after rows", typeof(UnambiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Ask), false, "token", typeof(AmbiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Ask), false, "status timeout", typeof(AmbiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Ask), false, "1080 fatal", typeof(AmbiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Ask), false, "1080 after rows", typeof(AmbiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.End), true, "token", typeof(AmbiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.End), true, "status timeout", typeof(AmbiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.End), true, "1080 fatal", typeof(AmbiguousTimeoutException))]
        [InlineData(null, false, "token", typeof(AmbiguousTimeoutException))]
        [InlineData(null, false, "status timeout", typeof(AmbiguousTimeoutException))]
        [InlineData(null, false, "1080 fatal", typeof(UnambiguousTimeoutException))]
        [InlineData(null, false, "1080 after rows", typeof(UnambiguousTimeoutException))]
        [InlineData(null, true, "token", typeof(UnambiguousTimeoutException))]
        [InlineData(null, true, "status timeout", typeof(UnambiguousTimeoutException))]
        public async Task QueryAsync_Timeout_MapsToTimeoutType(string operation, bool readOnly, string timeout, Type expected)
        {
            Func<HttpResponseMessage> response = timeout switch
            {
                "token" => () => throw new TaskCanceledException(),
                "status timeout" => JsonResponse(
                    """{"requestID":"r-1","errors":[{"code":1080,"msg":"Timeout 1s exceeded"}],"status":"timeout"}"""),
                "1080 fatal" => JsonResponse(
                    """{"requestID":"r-1","errors":[{"code":1080,"msg":"Timeout 1s exceeded"}],"status":"fatal"}"""),
                _ => JsonResponse(
                    """{"requestID":"r-1","results":[{"a":1}],"errors":[{"code":1080,"msg":"Timeout 1s exceeded"}],"status":"timeout"}""")
            };
            var node = QueryNode("uuid-a", "http://a:8093/query/service");
            var client = CreateQueryClient(QueryNodeProvider(node, node), new List<(Uri Uri, string Body)>(), response);

            var options = new QueryOptions { Conversational = Conversational(operation) }.Readonly(readOnly);

            async Task Run()
            {
                using var result = await client.QueryAsync<dynamic>("USING AI hello", options);
                await foreach (var row in result)
                {
                }
            }

            await Assert.ThrowsAsync(expected, Run);
        }

        [Theory]
        [InlineData(nameof(ConversationalOperation.Query), false, typeof(UnambiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Ask), false, typeof(AmbiguousTimeoutException))]
        [InlineData(nameof(ConversationalOperation.Pause), true, typeof(AmbiguousTimeoutException))]
        [InlineData(null, false, typeof(AmbiguousTimeoutException))]
        [InlineData(null, true, typeof(UnambiguousTimeoutException))]
        public async Task RetryAsync_Timeout_MapsToTimeoutType(string operation, bool readOnly, Type expected)
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var request = new QueryRequest
            {
                Options = new QueryOptions { Conversational = Conversational(operation) }.Readonly(readOnly),
                Token = cts.Token,
                Timeout = TimeSpan.FromSeconds(30)
            };

            var calls = 0;
            await Assert.ThrowsAsync(expected, () => CreateRetryOrchestrator().RetryAsync(() =>
            {
                calls++;
                return Task.FromResult<IQueryResult<dynamic>>(null);
            }, request));

            Assert.Equal(0, calls);
        }

        private static ConversationalRequest Conversational(string operation)
        {
            if (operation is null)
            {
                return null;
            }

            var op = (ConversationalOperation)Enum.Parse(typeof(ConversationalOperation), operation);
            return op == ConversationalOperation.Query
                ? new ConversationalRequest(op, null, null)
                : new ConversationalRequest(op, "chat-1", "uuid-a");
        }

        private static Func<HttpResponseMessage> JsonResponse(string json,
            HttpStatusCode statusCode = HttpStatusCode.OK) =>
            () => new HttpResponseMessage(statusCode) { Content = new StringContent(json) };

        private static HttpResponseMessage ConnectionReset() =>
            throw new HttpRequestException("Connection reset",
                new SocketException((int)SocketError.ConnectionReset));

        #endregion

        #region Conversational errors

        [Fact]
        public async Task QueryAsync_ChatNotFoundOnAsk_ThrowsChatCurrentlyUnavailableWithContext()
        {
            var node = QueryNode("uuid-a", "http://a:8093/query/service");
            var client = CreateQueryClient(QueryNodeProvider(node, node), new List<(Uri Uri, string Body)>(),
                JsonResponse("""{"requestID":"r-1","errors":[{"code":19236,"msg":"no chat found"}],"status":"fatal"}"""));

            var options = new QueryOptions { Conversational = Conversational(nameof(ConversationalOperation.Ask)) };

            var ex = await Assert.ThrowsAsync<ChatCurrentlyUnavailableException>(() =>
                client.QueryAsync<dynamic>("USING AI hello", options));

            Assert.Equal("chat-1", ex.ChatId);
            Assert.Equal("uuid-a", ex.OwnerNodeUuid);
            Assert.Equal("ask", ex.Operation);
            var context = Assert.IsType<QueryErrorContext>(ex.Context);
            var error = Assert.Single(context.Errors);
            Assert.Equal(19236, error.Code);
            Assert.Equal("no chat found", error.Message);
        }

        [Fact]
        public async Task QueryAsync_ChatNotFoundOnOrdinaryQuery_UsesOrdinaryMapping()
        {
            var client = CreateQueryClient(QueryNodeProvider(QueryNode("uuid-a", "http://a:8093/query/service")),
                new List<(Uri Uri, string Body)>(),
                JsonResponse("""{"requestID":"r-1","errors":[{"code":19236,"msg":"no chat found"}],"status":"fatal"}"""));

            var ex = await Assert.ThrowsAsync<CouchbaseException>(() =>
                client.QueryAsync<dynamic>("SELECT 1", new QueryOptions()));

            Assert.IsType<CouchbaseException>(ex);
        }

        private const string TestPassword = "test-password-6f1c";
        private const string TestApiKey = "test-api-key-9b2e";
        private const string TestConfigValue = "test-config-value-4d7a";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task QueryAsync_ConversationalError_DiagnosticsNeverContainTheModel(bool capella)
        {
            var model = capella
                ? ModelParameters.Create(new ModelOptions().Provider("openai").Name("gpt-4o"),
                    ModelCredential.Capella("user@example.com", TestPassword, "org-1"))
                : ModelParameters.Create(new ModelOptions().Provider("openai").Raw("organization", TestConfigValue),
                    ModelCredential.ApiKey(TestApiKey));
            var node = QueryNode("uuid-a", "http://a:8093/query/service");
            var sent = new List<(Uri Uri, string Body)>();
            var logger = new Mock<ILogger<QueryClient>>();
            logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            var client = CreateQueryClient(QueryNodeProvider(node, node), sent, logger.Object,
                JsonResponse("""{"requestID":"r-1","errors":[{"code":19236,"msg":"no chat found"}],"status":"fatal"}"""),
                JsonResponse("""{"requestID":"r-1","errors":[{"code":19241,"msg":"model auth failed"}],"status":"fatal"}"""));

            var exceptions = new List<CouchbaseException>();
            foreach (var expected in new[] { typeof(ChatCurrentlyUnavailableException), typeof(AuthenticationFailureException) })
            {
                var options = new QueryOptions().Parameter("p", 1).Raw("r", 2);
                options.Conversational = new ConversationalRequest(ConversationalOperation.Ask, "chat-1", "uuid-a", model);

                exceptions.Add((CouchbaseException)await Assert.ThrowsAsync(expected, () =>
                    client.QueryAsync<dynamic>("USING AI hello", options)));
            }

            // The secrets are in the request body, so the checks below are not vacuous.
            Assert.All(sent, x => Assert.Contains(capella ? TestPassword : TestApiKey, x.Body));

            var diagnostics = new List<string>();
            foreach (var ex in exceptions)
            {
                var context = Assert.IsType<QueryErrorContext>(ex.Context);
                Assert.Same(model, context.Conversational.Model);
                diagnostics.Add(context.Parameters);
                diagnostics.Add(ex.ToString());
                diagnostics.Add(context.ToString());
                diagnostics.Add(context.Conversational.ToString());
                diagnostics.Add(JsonSerializer.Serialize(context, QuerySerializerContext.Default.QueryErrorContext));
                diagnostics.Add(JsonSerializer.Serialize(context));
                diagnostics.Add(Newtonsoft.Json.JsonConvert.SerializeObject(context));
            }

            foreach (var invocation in logger.Invocations.Where(i => i.Method.Name == nameof(ILogger.Log)))
            {
                diagnostics.Add(invocation.Arguments[2]?.ToString());
                diagnostics.Add(invocation.Arguments[3]?.ToString());
            }

            Assert.NotEmpty(logger.Invocations);
            foreach (var text in diagnostics.Where(x => x is not null))
            {
                Assert.DoesNotContain(TestPassword, text);
                Assert.DoesNotContain(TestApiKey, text);
                Assert.DoesNotContain(TestConfigValue, text);
                Assert.DoesNotContain("natural_", text);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task QueryAsync_RawModelCredentials_NeverReachTheErrorContext(bool serverError)
        {
            var node = QueryNode("uuid-a", "http://a:8093/query/service");
            var sent = new List<(Uri Uri, string Body)>();
            var client = CreateQueryClient(QueryNodeProvider(node, node), sent, serverError
                ? JsonResponse("""{"requestID":"r-1","errors":[{"code":19241,"msg":"model auth failed"}],"status":"fatal"}""")
                : ConnectionReset);

            var options = new QueryOptions()
                .Raw("natural_cred", "user@example.com:" + TestPassword)
                .Raw("Natural_Config", new Dictionary<string, object> { ["api_key"] = TestApiKey });
            options.Conversational = Conversational(nameof(ConversationalOperation.Ask));

            var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(() =>
                client.QueryAsync<dynamic>("USING AI hello", options));

            Assert.IsType(serverError ? typeof(AuthenticationFailureException) : typeof(RequestCanceledException), ex);
            var body = Assert.Single(sent).Body;
            Assert.Contains(TestPassword, body);
            Assert.Contains(TestApiKey, body);

            var context = Assert.IsType<QueryErrorContext>(ex.Context);
            foreach (var text in new[]
                     {
                         context.Parameters, context.ToString(), ex.ToString(),
                         JsonSerializer.Serialize(context, QuerySerializerContext.Default.QueryErrorContext)
                     })
            {
                Assert.DoesNotContain(TestPassword, text);
                Assert.DoesNotContain(TestApiKey, text);
                Assert.DoesNotContain("natural_cred", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("natural_config", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion

        #region Conversational spans

        [Theory]
        [InlineData(null, "query")]
        [InlineData(nameof(ConversationalOperation.Query), "conversational_query")]
        [InlineData(nameof(ConversationalOperation.BeginChat), "conversational_query_begin_chat")]
        [InlineData(nameof(ConversationalOperation.Ask), "conversational_query_ask")]
        [InlineData(nameof(ConversationalOperation.Pause), "conversational_query_pause")]
        [InlineData(nameof(ConversationalOperation.Resume), "conversational_query_resume")]
        [InlineData(nameof(ConversationalOperation.End), "conversational_query_end")]
        public async Task QueryAsync_RootSpanName_FollowsTheConversationalOperation(string operation, string expected)
        {
            var node = QueryNode("uuid-a", "http://a:8093/query/service");
            var provider = QueryNodeProvider(node, node);
            var tracer = new RecordingTracer();
            var client = CreateQueryClient(provider, new List<(Uri Uri, string Body)>(),
                new Mock<ILogger<QueryClient>>().Object, tracer, SuccessResponse);

            var options = new QueryOptions();
            if (operation is not null)
            {
                options.Conversational = Conversational(operation);
            }

            using var result = await client.QueryAsync<dynamic>("SELECT 1", options);

            Assert.Equal(expected, Assert.Single(tracer.Names));
        }

        private sealed class RecordingTracer : IRequestTracer
        {
            public List<string> Names { get; } = new();

            public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
            {
                Names.Add(name);
                return NoopRequestSpan.Instance;
            }

            public IRequestTracer Start(TraceListener listener) => this;

            public void Dispose()
            {
            }
        }

        #endregion
    }
}
