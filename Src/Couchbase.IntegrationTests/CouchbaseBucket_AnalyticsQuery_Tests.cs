using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Couchbase.N1QL;
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

        [Test, Ignore("Analytics service is not currently discoverable. Until it is, we don't want to run these tests automatically.")]
        public void Can_execute_deferred_query()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var token = default(CancellationToken);
            Assert.AreEqual(CancellationToken.None, token);

            var query = new AnalyticsRequest(statement)
                .Deferred(true);

            var result = _bucket.Query<TestRequest>(query);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(QueryStatus.Running, result.Status);

            Assert.IsNotNull(result.Handle);

            var status = result.Handle.GetStatus();
            Assert.AreEqual(QueryStatus.Success, status);

            var rows = result.Handle.GetRows();
            Assert.IsNotEmpty(rows);
            Assert.AreEqual("hello", rows.First().Greeting);
        }

        [Test, Ignore("Analytics service is not currently discoverable. Until it is, we don't want to run these tests automatically.")]
        public async Task Can_execute_deferred_query_async()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var query = new AnalyticsRequest(statement)
                .Deferred(true);

            var result = await _bucket.QueryAsync<TestRequest>(query);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(QueryStatus.Running, result.Status);

            Assert.IsNotNull(result.Handle);

            var status = await result.Handle.GetStatusAsync();
            Assert.AreEqual(QueryStatus.Success, status);

            var rows = await result.Handle.GetRowsAsync();
            Assert.IsNotEmpty(rows);
            Assert.AreEqual("hello", rows.First().Greeting);
        }
    }
}
