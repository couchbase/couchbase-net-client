using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.N1QL;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.UnitTests.Analytics
{
    [TestFixture]
    public class AnalyticsClientTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            ConfigContextBase.AnalyticsUris.Add(new FailureCountingUri("http://localhost"));
        }

        [Test]
        public void Query_Sets_LastActivity()
        {
            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var config = new ClientConfiguration();
            var client = new AnalyticsClient(httpClient, new JsonDataMapper(config), config);
            Assert.IsNull(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            client.Query<dynamic>(queryRequest);

            Assert.IsNotNull(client.LastActivity);
        }

        [Test]
        public async Task QueryAsync_Sets_LastActivity()
        {
            var httpClient = new HttpClient(
                FakeHttpMessageHandler.Create(request => new HttpResponseMessage(HttpStatusCode.OK))
            );

            var config = new ClientConfiguration();
            var client = new AnalyticsClient(httpClient, new JsonDataMapper(config), config);
            Assert.IsNull(client.LastActivity);

            var queryRequest = new AnalyticsRequest("SELECT * FROM `default`;");
            await client.QueryAsync<dynamic>(queryRequest, CancellationToken.None);

            Assert.IsNotNull(client.LastActivity);
        }
    }
}
