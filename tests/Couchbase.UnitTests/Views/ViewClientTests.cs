using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Views
{
    public class ViewClientTests
    {
        [Fact]
        public async Task ViewClient_Submits_ViewQuery_Using_Post()
        {
            var keys = Enumerable.Range(1, 10).Select(i => $"key-{i}").ToList();
            var expectedJson = JsonConvert.SerializeObject(new
            {
                keys
            }, Formatting.None);

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                // verify request was a post
                Assert.Equal(HttpMethod.Post, request.Method);

                // get the post body and verify content
                var content = request.Content.ReadAsStringAsync().Result;
                Assert.Equal(expectedJson, content);

                // return empty json response
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ }")
                };
            });

            var httpClient = new CouchbaseHttpClient(handler);
            var serializer = new DefaultSerializer();
            var queryClient = new ViewClient(httpClient, serializer, new Mock<ILogger<ViewClient>>().Object,
                new Mock<IRedactor>().Object, NullRequestTracer.Instance);

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys(keys);

            await queryClient.ExecuteAsync<dynamic, dynamic>(query).ConfigureAwait(false);
        }

        [Fact]
        public async Task ExecuteAsync_Sets_LastActivity()
        {
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ }")
            });

            var httpClient = new CouchbaseHttpClient(handler);
            var serializer = new DefaultSerializer();
            var queryClient = new ViewClient(httpClient, serializer, new Mock<ILogger<ViewClient>>().Object,
                new Mock<IRedactor>().Object, NullRequestTracer.Instance);

            Assert.Null(queryClient.LastActivity);

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys("test-key");

            await queryClient.ExecuteAsync<dynamic, dynamic>(query).ConfigureAwait(false);
            Assert.NotNull(queryClient.LastActivity);
        }

        [Fact]
        public async Task ExecuteAsync_SerializerOverride_UsesOverride()
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

            var httpClient = new CouchbaseHttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost:8091")
            };

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomViewsUri(It.IsAny<string>()))
                .Returns(new Uri("http://localhost:8092"));

            var primarySerializer = new Mock<ITypeSerializer> {DefaultValue = DefaultValue.Mock};
            var overrideSerializer = new Mock<ITypeSerializer> {DefaultValue = DefaultValue.Mock};

            var client = new ViewClient(httpClient, primarySerializer.Object, new Mock<ILogger<ViewClient>>().Object,
                new Mock<IRedactor>().Object, NullRequestTracer.Instance);

            await client.ExecuteAsync<object, object>(new ViewQuery("default", "doc", "view")
            {
                Serializer = overrideSerializer.Object
            }).ConfigureAwait(false);

            primarySerializer.Verify(
                m => m.DeserializeAsync<BlockViewResult<object, object>.ViewResultData>(It.IsAny<Stream>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
            overrideSerializer.Verify(
                m => m.DeserializeAsync<BlockViewResult<object, object>.ViewResultData>(It.IsAny<Stream>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce);
        }
    }

    public class Beer
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("abv")] public decimal Abv { get; set; }

        [JsonProperty("ibu")] public decimal Ibu { get; set; }

        [JsonProperty("srm")] public decimal Srm { get; set; }

        [JsonProperty("upc")] public int Upc { get; set; }

        [JsonProperty("type")] public string Type { get; set; }

        [JsonProperty("brewery_id")] public string BreweryId { get; set; }

        [JsonProperty("description")] public string Description { get; set; }

        [JsonProperty("style")] public string Style { get; set; }

        [JsonProperty("category")] public string Category { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
