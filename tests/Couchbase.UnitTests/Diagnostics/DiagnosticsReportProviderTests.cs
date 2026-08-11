using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Diagnostics;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
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
        public async Task PingHttpServiceAsync_Without_A_Token_Uses_The_Fallback_Timeout()
        {
            //arrange

            var factory = CreateFactory(_ => new HttpResponseMessage(HttpStatusCode.OK));

            //act, assert

            // A generous fallback must not cancel a healthy ping.
            await DiagnosticsReportProvider.PingHttpServiceAsync(factory, new Uri("http://node1:8093/admin/ping"),
                TimeSpan.FromMinutes(1), CancellationToken.None);
        }

        private static MockHttpClientFactory CreateFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            new(() => new HttpClient(FakeHttpMessageHandler.Create(handler)));
    }
}
