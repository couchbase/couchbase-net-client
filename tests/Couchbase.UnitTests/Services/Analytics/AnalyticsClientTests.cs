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
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Analytics
{
    public class AnalyticsClientTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Client_sets_AnalyticsPriority_Header(bool priority)
        {
            var options = new ClusterOptions().WithServers("http://localhost");
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

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(new DefaultSerializer()), context);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            queryRequest.Priority(priority);

            client.Query<dynamic>(queryRequest);
        }

        [Fact]
        public void Query_Sets_LastActivity()
        {
            var options = new ClusterOptions().WithServers("http://localhost");
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

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(new DefaultSerializer()), context);

            Assert.Null(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            client.Query<dynamic>(queryRequest);

            Assert.NotNull(client.LastActivity);
        }

        [Fact]
        public async Task QueryAsync_Sets_LastActivity()
        {
            var options = new ClusterOptions().WithServers("http://localhost");
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

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(new DefaultSerializer()), context);

            Assert.Null(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            await client.QueryAsync<dynamic>(queryRequest, CancellationToken.None).ConfigureAwait(false);

            Assert.NotNull(client.LastActivity);
        }
    }
}
