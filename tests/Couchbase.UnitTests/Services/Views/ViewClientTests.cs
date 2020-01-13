using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Views
{
    public class ViewClientTests
    {
        private const string ViewResultResourceName = @"Services\Views\view_result.json";

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

            var httpClient = new HttpClient(handler);
            var queryClient = new ViewClient(httpClient,
                new JsonDataMapper(new DefaultSerializer()),
                new ClusterContext(null, new ClusterOptions()));

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys(keys);

            await queryClient.ExecuteAsync(query).ConfigureAwait(false);
        }

        [Fact]
        public async Task ExecuteAsync_Sets_LastActivity()
        {
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ }")
            });

            var httpClient = new HttpClient(handler);
            var queryClient = new ViewClient(httpClient,
                new JsonDataMapper(new DefaultSerializer()),
                new ClusterContext(null, new ClusterOptions()));

            Assert.Null(queryClient.LastActivity);

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys("test-key");
            query.UseStreaming(true);

            await queryClient.ExecuteAsync(query);
            Assert.NotNull(queryClient.LastActivity);
        }

        [Fact]
        public async Task Test_Count()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            var result = await response.CountAsync();

            Assert.Equal(10, result);
        }

        [Fact]
        public async Task Test_Any()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            var result = await response.AnyAsync();

            Assert.True(result);
        }

        [Fact]
        public async Task Test_First()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            var first = await response.FirstAsync();
            Assert.NotNull(first);
        }

        [Fact]
        public async Task Test_Enumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            //read the results
            var count = 0;
            await foreach (var beer in response)
            {
                count++;
            }
            Assert.Equal(10, count);
        }

        [Fact]
        public async Task Test_Repeat_Enumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            Assert.Equal(10, (await response.ToListAsync()).Count);
            await Assert.ThrowsAsync<StreamAlreadyReadException>(() => response.ToListAsync().AsTask());
        }

        [Fact]
        public async Task Test_Values()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            Assert.Equal(10, await response.CountAsync());
        }

        [Fact]
        public void Test_Success()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Test_StatusCode()
        {
            const HttpStatusCode statusCode = HttpStatusCode.Accepted;
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(statusCode, string.Empty, stream);

            Assert.Equal(statusCode, response.StatusCode);
        }

        [Fact]
        public void Test_Message()
        {
            const string message = "message";
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, message, stream);

            Assert.Equal(message, response.Message);
        }

        [Fact]
        public async Task Test_TotalRows()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult(HttpStatusCode.OK, string.Empty, stream);

            await foreach (var row in response)
            {
                // noop
            }

            Assert.Equal(7303u, response.MetaData.TotalRows);
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
