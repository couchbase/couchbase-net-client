using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
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
                new ClusterOptions());

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys(keys);

            await queryClient.ExecuteAsync<dynamic>(query).ConfigureAwait(false);
        }

        [Fact]
        public void Execute_Sets_LastActivity()
        {
            var handler = FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ }")
            });

            var httpClient = new HttpClient(handler);
            var queryClient = new ViewClient(httpClient,
                new JsonDataMapper(new DefaultSerializer()),
                new ClusterOptions());
            Assert.Null(queryClient.LastActivity);

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys("test-key");
            query.UseStreaming(true);

            queryClient.Execute<dynamic>(query);
            Assert.NotNull(queryClient.LastActivity);
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
                new ClusterOptions());

            Assert.Null(queryClient.LastActivity);

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys("test-key");
            query.UseStreaming(true);

            await queryClient.ExecuteAsync<dynamic>(query);
            Assert.NotNull(queryClient.LastActivity);
        }

        [Fact]
        public void Test_Count()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            Assert.Equal(10, response.Rows.Count());
        }

        [Fact]
        public void Test_Any()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            Assert.True(response.Rows.Any());
        }

        [Fact]
        public void Test_First()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            var first = response.Rows.First();
            Assert.NotNull(first);
        }

        [Fact]
        public void Test_Enumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            //read the results
            var count = 0;
            foreach (var beer in response.Rows)
            {
                count++;
            }
            Assert.Equal(10, count);
        }

        [Fact]
        public void Test_Repeat_Enumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            Assert.Equal(10, response.Rows.ToList().Count);
            Assert.Throws<StreamAlreadyReadException>(() => response.Rows.ToList());
        }

        [Fact]
        public void Test_Values()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            Assert.Equal(10, response.Rows.Count());
        }

        [Fact]
        public void Test_Success()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Test_StatusCode()
        {
            const HttpStatusCode statusCode = HttpStatusCode.Accepted;
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(statusCode, string.Empty, stream);

            Assert.Equal(statusCode, response.StatusCode);
        }

        [Fact]
        public void Test_Message()
        {
            const string message = "message";
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, message, stream);

            Assert.Equal(message, response.Message);
        }

        [Fact]
        public void Test_TotalRows()
        {
            var stream = ResourceHelper.ReadResourceAsStream(ViewResultResourceName);
            var response = new ViewResult<Beer>(HttpStatusCode.OK, string.Empty, stream);

            foreach (var row in response.Rows)
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
