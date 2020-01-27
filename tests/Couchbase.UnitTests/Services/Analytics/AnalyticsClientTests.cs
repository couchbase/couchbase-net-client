using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.Serializers;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Services.Analytics
{
    public class AnalyticsClientTests
    {
        [Theory]
        [InlineData(typeof(DefaultSerializer))]
        [InlineData(typeof(NonStreamingSerializer))]
        public async Task TestSuccess(Type serializerType)
        {
            using var response = ResourceHelper.ReadResourceAsStream(@"Documents\Analytics\good-request.json");

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
            var options = new ClusterOptions().Bucket("default").Servers("http://localhost:8901");
            var context = new ClusterContext(null, options);

            var clusterNode = new ClusterNode(context)
            {
                EndPoint = new Uri("http://localhost:8091").GetIpEndPoint(8091, false),
                NodesAdapter = new NodeAdapter(new Node {Hostname = "127.0.0.1"},
                    new NodesExt {Hostname = "127.0.0.1", Services = new Couchbase.Core.Configuration.Server.Services
                    {
                        Cbas = 8095
                    }}, new BucketConfig())
            };
            clusterNode.BuildServiceUris();
            context.AddNode(clusterNode);

            var serializer = (ITypeSerializer) Activator.CreateInstance(serializerType);
            var client = new AnalyticsClient(httpClient, new JsonDataMapper(serializer), serializer, context);

            var result = await client.QueryAsync<dynamic>(new AnalyticsRequest("SELECT * FROM `default`"), default);

            Assert.Equal(5, await result.CountAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Client_sets_AnalyticsPriority_Header(bool priority)
        {
            var options = new ClusterOptions().Servers("http://localhost");
            var context = new ClusterContext(null, options);
            context.AddNode(new ClusterNode(context)
            {
                AnalyticsUri = new Uri("http://localhost:8094/query"),
                EndPoint = new IPEndPoint(IPAddress.Loopback, 8091),
                NodesAdapter = new NodeAdapter { Analytics = 8094 }
            });

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

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClient, new JsonDataMapper(serializer), serializer, context);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            queryRequest.Priority(priority);

            await client.QueryAsync<dynamic>(queryRequest);
        }

        [Fact]
        public async Task QueryAsync_Sets_LastActivity()
        {
            var options = new ClusterOptions().Servers("http://localhost");
            var context = new ClusterContext(null, options);
            context.AddNode(new ClusterNode(new ClusterContext(null, new ClusterOptions()))
            {
                AnalyticsUri = new Uri("http://localhost:8094/query"),
                EndPoint = new IPEndPoint(IPAddress.Loopback, 8091),
                NodesAdapter = new NodeAdapter { Analytics = 8094 }
            });

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })
            );

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClient, new JsonDataMapper(serializer), serializer, context);

            Assert.Null(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            await client.QueryAsync<dynamic>(queryRequest, CancellationToken.None).ConfigureAwait(false);

            Assert.NotNull(client.LastActivity);
        }
    }
}
