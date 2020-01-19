using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
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
                            N1Ql = 8093
                        }}, new BucketConfig())
                };
                clusterNode.BuildServiceUris();
                context.AddNode(clusterNode);

                var serializer = new DefaultSerializer();
                var client = new QueryClient(httpClient, new JsonDataMapper(serializer), serializer, context);

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

        [Fact]
        public void EnhancedPreparedStatements_defaults_to_false()
        {
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
            var client = new QueryClient(context);
            Assert.False(client.EnhancedPreparedStatementsEnabled);
        }

        [Fact]
        public void EnhancedPreparedStatements_is_set_to_true_if_enabled_in_cluster_caps()
        {
            var context = new ClusterContext(new CancellationTokenSource(), new ClusterOptions());
            var client = new QueryClient(context);
            Assert.False(client.EnhancedPreparedStatementsEnabled);

            var clusterCapabilities = new ClusterCapabilities();
            clusterCapabilities.Capabilities = new Dictionary<string, IEnumerable<string>>
            {
                {
                    ServiceType.Query.GetDescription(),
                    new List<string> {ClusterCapabilityFeatures.EnhancedPreparedStatements.GetDescription()}
                }
            };

            client.UpdateClusterCapabilities(clusterCapabilities);
            Assert.True(client.EnhancedPreparedStatementsEnabled);
        }
    }
}
