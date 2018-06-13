using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.N1QL;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
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
    }
}
