using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.N1QL;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Analytics
{
    [TestFixture]
    public class AnalyticsClientTests
    {
        [Test]
        public void Query_Sets_LastActivity()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(context.ClientConfig),
                context);

            Assert.IsNull(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            client.Query<dynamic>(queryRequest);

            Assert.IsNotNull(client.LastActivity);
        }

        [Test]
        public async Task QueryAsync_Sets_LastActivity()
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(context.ClientConfig),
                context);

            Assert.IsNull(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            await client.QueryAsync<dynamic>(queryRequest, CancellationToken.None);

            Assert.IsNotNull(client.LastActivity);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Client_sets_AnalyticsPriority_Header(bool priority)
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request =>
                {
                    if (priority)
                    {
                        Assert.IsTrue(request.Headers.TryGetValues(AnalyticsClient.AnalyticsPriorityHeaderName, out var values));
                        Assert.AreEqual("-1", values.First());
                    }
                    else
                    {
                        Assert.IsFalse(request.Headers.TryGetValues(AnalyticsClient.AnalyticsPriorityHeaderName, out _));
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK);
                })
            );

            var client = new AnalyticsClient(httpClient, new JsonDataMapper(context.ClientConfig), context);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            queryRequest.Priority(priority);

            client.Query<dynamic>(queryRequest);
        }

        [Test]
        public void When_deferred_is_true_query_result_is_DeferredAnalyticsResult()
        {
            var resultJson = JsonConvert.SerializeObject(new
            {
                status = "Success",
                handle = "handle"
            });

            var context = ContextFactory.GetCouchbaseContext();
            context.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resultJson)
                })
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(context.ClientConfig),
                context);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            var result = client.Query<dynamic>(queryRequest);

            Assert.IsInstanceOf<AnalyticsDeferredResultHandle<dynamic>>(result.Handle);
            Assert.AreEqual(QueryStatus.Success, result.Status);

            var deferredResult = (AnalyticsDeferredResultHandle<dynamic>) result.Handle;
            Assert.AreEqual("handle", deferredResult.HandleUri);
        }

        [Test]
        public void Can_export_deferred_handle()
        {
            const string handleUri = "/analytics/service/status/3-0";
            const string expectedJson = "{\"v\":1,\"uri\":\"/analytics/service/status/3-0\"}";
            var handle = new AnalyticsDeferredResultHandle<dynamic>(null, null, null, handleUri);

            var context = ContextFactory.GetCouchbaseContext();
            context.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(context.ClientConfig),
                context);

            var encodedHandle = client.ExportDeferredQueryHandle(handle);
            Assert.AreEqual(expectedJson, encodedHandle);
        }

        [Test]
        public void Can_import_deferred_handle()
        {
            const string expectedHandle = "/analytics/service/status/3-0";
            const string json = "{\"v\":1,\"uri\":\"/analytics/service/status/3-0\"}";

            var context = ContextFactory.GetCouchbaseContext();
            context.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(context.ClientConfig),
                context);

            var handle = client.ImportDeferredQueryHandle<dynamic>(json);
            Assert.IsNotNull(handle);
            Assert.AreEqual(expectedHandle, (handle as AnalyticsDeferredResultHandle<dynamic>).HandleUri);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Import_throws_exception_when_json_is_invalid(string handleUri)
        {
            var context = ContextFactory.GetCouchbaseContext();
            context.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));

            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var client = new AnalyticsClient(httpClient,
                new JsonDataMapper(context.ClientConfig),
                context);

            var json = JsonConvert.SerializeObject(new {v = 1, uri = handleUri});
            Assert.Throws<ArgumentException>(() => client.ImportDeferredQueryHandle<dynamic>(json));
        }
    }
}
