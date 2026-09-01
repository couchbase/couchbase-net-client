using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.HTTP;
using Couchbase.Diagnostics;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Diagnostics
{
    public class DiagnosticsReportProviderTests
    {
        [Theory]
        [InlineData("http://node1:8093/query/service", "/admin/ping", "http://node1:8093/admin/ping")]
        [InlineData("http://node1:8095/analytics/service", "/admin/ping", "http://node1:8095/admin/ping")]
        [InlineData("http://node1:8094/", "/api/ping", "http://node1:8094/api/ping")]
        [InlineData("https://node1:18093/query/service", "/admin/ping", "https://node1:18093/admin/ping")]
        public void BuildPingUri_Keeps_The_Node_Host_And_Port(string serviceUri, string pingPath, string expected)
        {
            //act

            var uri = DiagnosticsReportProvider.BuildPingUri(new Uri(serviceUri), pingPath);

            //assert

            Assert.Equal(expected, uri.ToString());
        }

        [Fact]
        public void BuildPingUri_Is_Null_Without_A_Service_Uri()
        {
            Assert.Null(DiagnosticsReportProvider.BuildPingUri(null, DiagnosticsReportProvider.AdminPingPath));
        }

        [Fact]
        public async Task PingHttpServiceAsync_Requests_The_Given_Uri()
        {
            //arrange

            Uri requested = null;
            var factory = CreateFactory(request =>
            {
                requested = request.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            //act

            await DiagnosticsReportProvider.PingHttpServiceAsync(factory, new Uri("http://node1:8093/admin/ping"),
                TimeSpan.FromSeconds(10), CancellationToken.None);

            //assert

            Assert.Equal("http://node1:8093/admin/ping", requested.ToString());
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task PingHttpServiceAsync_Throws_On_A_Failure_Status(HttpStatusCode statusCode)
        {
            //arrange

            var factory = CreateFactory(_ => new HttpResponseMessage(statusCode));

            //act, assert

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                DiagnosticsReportProvider.PingHttpServiceAsync(factory, new Uri("http://node1:8093/admin/ping"),
                    TimeSpan.FromSeconds(10), CancellationToken.None));
        }

        [Fact]
        public async Task PingHttpServiceAsync_Honours_The_Supplied_Token()
        {
            //arrange

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var factory = CreateFactory(_ => new HttpResponseMessage(HttpStatusCode.OK));

            //act, assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                DiagnosticsReportProvider.PingHttpServiceAsync(factory, new Uri("http://node1:8093/admin/ping"),
                    TimeSpan.FromMinutes(1), cts.Token));
        }

        [Fact]
        public async Task PingHttpServiceAsync_Without_A_Token_Uses_The_Service_Timeout()
        {
            //arrange

            var factory = CreateFactory(_ => new HttpResponseMessage(HttpStatusCode.OK));

            //act, assert

            // A generous timeout must not cancel a healthy ping.
            await DiagnosticsReportProvider.PingHttpServiceAsync(factory, new Uri("http://node1:8093/admin/ping"),
                TimeSpan.FromMinutes(1), CancellationToken.None);
        }

        [Fact]
        public async Task PingHttpServiceAsync_Applies_The_Service_Timeout_To_A_Live_Token()
        {
            //arrange

            using var external = new CancellationTokenSource();
            var factory = new MockHttpClientFactory(() =>
                new HttpClient(new DelayingHttpMessageHandler(TimeSpan.FromMinutes(1))));

            //act, assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                DiagnosticsReportProvider.PingHttpServiceAsync(factory, new Uri("http://node1:8093/admin/ping"),
                    TimeSpan.FromMilliseconds(50), external.Token));

            Assert.False(external.IsCancellationRequested);
        }

        [Fact]
        public async Task CreatePingReportAsync_Reports_Host_And_Port_As_The_Remote()
        {
            //arrange

            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.AddClusterService<ICouchbaseHttpClientFactory, MockHttpClientFactory>(
                CreateFactory(_ => new HttpResponseMessage(HttpStatusCode.OK)));

            var context = new ClusterContext(null, options);
            var node = new Mock<IClusterNode>();
            node.SetupGet(x => x.HasQuery).Returns(true);
            node.SetupGet(x => x.QueryUri).Returns(new Uri("http://node1:8093/query/service"));
            node.SetupGet(x => x.EndPoint).Returns(new HostEndpointWithPort("node1", 8093));
            context.AddNode(node.Object);

            //act

            var report = await DiagnosticsReportProvider.CreatePingReportAsync(context, null,
                new PingOptions { ServiceTypesValue = new[] { ServiceType.Query } });

            //assert

            var entry = Assert.Single(report.Services["n1ql"]);
            Assert.Equal("node1:8093", entry.Remote);
        }

        private static MockHttpClientFactory CreateFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            new(() => new HttpClient(FakeHttpMessageHandler.Create(handler)));

        /// <summary>
        /// Stands in for a node that accepts the connection and never answers.
        /// </summary>
        private sealed class DelayingHttpMessageHandler : HttpMessageHandler
        {
            private readonly TimeSpan _delay;

            public DelayingHttpMessageHandler(TimeSpan delay)
            {
                _delay = delay;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        }
    }
}
