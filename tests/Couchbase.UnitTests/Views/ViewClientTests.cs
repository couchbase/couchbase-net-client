using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
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
            var queryClient = new ViewClient(httpClient, serializer, new Mock<ILogger<ViewClient>>().Object);

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
            var queryClient = new ViewClient(httpClient, serializer, new Mock<ILogger<ViewClient>>().Object);

            Assert.Null(queryClient.LastActivity);

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys("test-key");
            query.UseStreaming(true);

            await queryClient.ExecuteAsync<dynamic, dynamic>(query);
            Assert.NotNull(queryClient.LastActivity);
        }
    }

    public class Beer
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("abv")]
        public decimal Abv { get; set; }

        [JsonProperty("ibu")]
        public decimal Ibu { get; set; }

        [JsonProperty("srm")]
        public decimal Srm { get; set; }

        [JsonProperty("upc")]
        public int Upc { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("brewery_id")]
        public string BreweryId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("style")]
        public string Style { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
