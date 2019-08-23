using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.Serializers;
using Couchbase.Services.Analytics;
using Couchbase.UnitTests.Utils;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Analytics
{
    public class AnalyticsClientTests
    {
        [Fact]
        public void Query_Sets_LastActivity()
        {
            var configuration = new ClusterOptions().WithServers("http://localhost");
            configuration.GlobalNodes.Add(new ClusterNode{ AnalyticsUri = new Uri("http://localhost:8094/query")});

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })
            );

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(new DefaultSerializer()), configuration);

            Assert.Null(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            client.Query<dynamic>(queryRequest);

            Assert.NotNull(client.LastActivity);
        }

        [Fact]
        public async Task QueryAsync_Sets_LastActivity()
        {
            var configuration = new ClusterOptions().WithServers("http://localhost");
            configuration.GlobalNodes.Add(new ClusterNode{ AnalyticsUri = new Uri("http://localhost:8094/query")});

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })
            );

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(new DefaultSerializer()), configuration);

            Assert.Null(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            await client.QueryAsync<dynamic>(queryRequest, CancellationToken.None).ConfigureAwait(false);

            Assert.NotNull(client.LastActivity);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Client_sets_AnalyticsPriority_Header(bool priority)
        {
            var configuration = new ClusterOptions().WithServers("http://localhost");
            configuration.GlobalNodes.Add(new ClusterNode{ AnalyticsUri = new Uri("http://localhost:8094/query")});

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request =>
                {
                    if (priority)
                    {
                        Assert.True(request.Headers.TryGetValues(AnalyticsClient.AnalyticsPriorityHeaderName, out var values));
                        Assert.Equal("-1", values.First());
                    }
                    else
                    {
                        Assert.False(request.Headers.TryGetValues(AnalyticsClient.AnalyticsPriorityHeaderName, out _));
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}")};
                })
            );

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(new DefaultSerializer()), configuration);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            queryRequest.Priority(priority);

            client.Query<dynamic>(queryRequest);
        }

        [Fact]
        public void When_deferred_is_true_query_result_is_DeferredAnalyticsResult()
        {
            var resultJson = JsonConvert.SerializeObject(new
            {
                status = "Success",
                handle = "handle"
            });

            var configuration = new ClusterOptions().WithServers("http://localhost");
            configuration.GlobalNodes.Add(new ClusterNode{ AnalyticsUri = new Uri("http://localhost:8094/query")});

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resultJson)
                })
            );

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(new DefaultSerializer()), configuration);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            var result = client.Query<dynamic>(queryRequest);

            Assert.IsType<AnalyticsDeferredResultHandle<dynamic>>(result.Handle);
            Assert.Equal(QueryStatus.Success, result.MetaData.Status);

            var deferredResult = (AnalyticsDeferredResultHandle<dynamic>)result.Handle;
            Assert.Equal("handle", deferredResult.HandleUri);
        }

        [Fact]
        public void Can_export_deferred_handle()
        {
            const string handleUri = "/analytics/service/status/3-0";
            const string expectedJson = "{\"v\":1,\"uri\":\"/analytics/service/status/3-0\"}";
            var handle = new AnalyticsDeferredResultHandle<dynamic>(null, null, null, handleUri);

            var configuration = new ClusterOptions();
            configuration.WithServers("http://localhost");

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(new DefaultSerializer()),
                configuration);

            var encodedHandle = client.ExportDeferredQueryHandle(handle);
            Assert.Equal(expectedJson, encodedHandle);
        }

        [Fact]
        public void Can_import_deferred_handle()
        {
            const string expectedHandle = "/analytics/service/status/3-0";
            const string json = "{\"v\":1,\"uri\":\"/analytics/service/status/3-0\"}";

            var configuration = new ClusterOptions().WithServers("http://localhost");

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(new DefaultSerializer()),
                configuration);

            var handle = client.ImportDeferredQueryHandle<dynamic>(json);
            Assert.NotNull(handle);
            Assert.Equal(expectedHandle, (handle as AnalyticsDeferredResultHandle<dynamic>).HandleUri);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Import_throws_exception_when_json_is_invalid(string handleUri)
        {
            var configuration = new ClusterOptions().WithServers("http://localhost");

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(new DefaultSerializer()),
                configuration);

            var json = JsonConvert.SerializeObject(new {v = 1, uri = handleUri});
            Assert.Throws<ArgumentException>(() => client.ImportDeferredQueryHandle<dynamic>(json));
        }
    }
}
