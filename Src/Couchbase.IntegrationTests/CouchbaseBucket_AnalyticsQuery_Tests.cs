using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Newtonsoft.Json;
using NUnit.Framework;
using Couchbase.Analytics.Ingestion;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucketAnalyticsQueryTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetConfiguration("beer-sample"));
            _cluster.SetupEnhancedAuth();

            _bucket = _cluster.OpenBucket("default");
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
        }

        private class TestRequest
        {
            [JsonProperty("greeting")]
            public string Greeting { get; set; }
        }

        [Test, Ignore("Analytics service is not currently discoverable. Until it is, we don't want to run these tests automatically.")]
        public void Execute_Query()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var query = new AnalyticsRequest(statement);

            var result = _bucket.Query<TestRequest>(query);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("hello", result.Rows.First().Greeting);
        }

        [Test, Ignore("Analytics service is not currently discoverable. Until it is, we don't want to run these tests automatically.")]
        public async Task Execute_Query_Async()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var query = new AnalyticsRequest(statement);

            var result = await _bucket.QueryAsync<TestRequest>(query, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("hello", result.Rows.First().Greeting);
        }

        [Test, Ignore("Analytics service is not currently discoverable. Until it is, we don't want to run these tests automatically.")]
        public async Task Test_Ingest()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var query = new AnalyticsRequest(statement);

            var result = await _bucket.IngestAsync<dynamic>(query, new IngestOptions()).ConfigureAwait(false);

            Assert.True(result.Count > 0);
        }
    }
}
