using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Search;
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

            var httpClient = new CouchbaseHttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsUri())
                .Returns(new Uri("http://localhost:8096"));

            var serializer = (ITypeSerializer) Activator.CreateInstance(serializerType);
            var client = new AnalyticsClient(httpClient, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object);

            var result = await client.QueryAsync<dynamic>(new AnalyticsRequest("SELECT * FROM `default`"), default);

            Assert.Equal(5, await result.CountAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Client_sets_AnalyticsPriority_Header(bool priority)
        {
            var httpClient = new CouchbaseHttpClient(
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

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsUri())
                .Returns(new Uri("http://localhost:8096"));

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClient, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            queryRequest.Priority(priority);

            await client.QueryAsync<dynamic>(queryRequest);
        }

        [Fact]
        public async Task QueryAsync_Sets_LastActivity()
        { var httpClient = new CouchbaseHttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                })
            );

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomAnalyticsUri())
                .Returns(new Uri("http://localhost:8096"));

            var serializer = new DefaultSerializer();
            var client = new AnalyticsClient(httpClient, mockServiceUriProvider.Object, serializer,
                new Mock<ILogger<AnalyticsClient>>().Object);

            Assert.Null(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            await client.QueryAsync<dynamic>(queryRequest, CancellationToken.None).ConfigureAwait(false);

            Assert.NotNull(client.LastActivity);
        }
    }
}
