using System.Linq;
using System.Net;
using System.Net.Http;
using Couchbase.Core.Serialization;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Views
{
    [TestFixture]
    public class ViewClientTests
    {
        [Test]
        public void ViewClient_Submits_ViewQuery_Using_Post()
        {
            var keys = Enumerable.Range(1, 10).Select(i => $"key-{i}").ToList();
            var expectedJson = JsonConvert.SerializeObject(new
            {
                keys
            }, Formatting.None);

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                // verify request was a post
                Assert.AreEqual(HttpMethod.Post, request.Method);

                // get the post body and verify content
                var content = request.Content.ReadAsStringAsync().Result;
                Assert.AreEqual(expectedJson, content);

                // return empty json response
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ }")
                };
            });

            var httpClient = new HttpClient(handler);
            var queryClient = new ViewClient(httpClient, new JsonDataMapper(new DefaultSerializer()));

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys(keys);

            var result = queryClient.Execute<dynamic>(query);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void ViewClient_Submits_Streaming_ViewQuery_Using_Post()
        {
            var keys = Enumerable.Range(1, 10).Select(i => $"key-{i}").ToList();
            var expectedJson = JsonConvert.SerializeObject(new
            {
                keys
            }, Formatting.None);

            var handler = FakeHttpMessageHandler.Create(request =>
            {
                // verify request was a post
                Assert.AreEqual(HttpMethod.Post, request.Method);

                // get the post body and verify content
                var content = request.Content.ReadAsStringAsync().Result;
                Assert.AreEqual(expectedJson, content);

                // return empty json response
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ }")
                };
            });

            var httpClient = new HttpClient(handler);
            var queryClient = new StreamingViewClient(httpClient, new JsonDataMapper(new DefaultSerializer()));

            var query = new ViewQuery("bucket-name", "http://localhost");
            query.Keys(keys);
            query.UseStreaming(true);

            var result = queryClient.Execute<dynamic>(query);
            Assert.IsTrue(result.Success);
        }

    }
}
