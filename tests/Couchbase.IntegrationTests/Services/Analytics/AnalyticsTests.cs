using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management.Analytics;
using Couchbase.Management.Collections;
using Couchbase.Query;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Analytics
{
    public class AnalyticsTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private static string scopeName = "myScope" + randomString();
        private static string collectionName = "myCollection" + randomString();

        private static string randomString()
        {
            {
                int length = 7;

                // creating a StringBuilder object()
                StringBuilder str_build = new StringBuilder();
                Random random = new Random();

                char letter;

                for (int i = 0; i < length; i++)
                {
                    double flt = random.NextDouble();
                    int shift = Convert.ToInt32(Math.Floor(25 * flt));
                    letter = Convert.ToChar(shift + 65);
                    str_build.Append(letter);
                }
                return str_build.ToString();
            }
        }

        public AnalyticsTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        private class TestRequest
        {
            [JsonProperty("greeting")]
            public string Greeting { get; set; }
        }

        [Fact]
        public async Task Execute_Query()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var analyticsResult = await cluster.AnalyticsQueryAsync<TestRequest>(statement).ConfigureAwait(false);
            var result = await analyticsResult.ToListAsync();

            Assert.Single(result);
            Assert.Equal("hello", result.First().Greeting);
        }

        [Fact]
        public async Task Test_Ingest()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var result = await cluster.IngestAsync<dynamic>(
                statement,
                await _fixture.GetDefaultCollection().ConfigureAwait(false),
                options =>
                {
                    options.Timeout(TimeSpan.FromSeconds(75));
                    options.Expiry(TimeSpan.FromDays(1));
                }
            ).ConfigureAwait(false);

            Assert.True(result.Any());
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_Collections_DataverseCollectionQuery()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);
            string dataverseName = bucket.Name + "." + scopeName;
            var collectionManager = (CollectionManager)bucket.Collections;
            var scopeSpec = new ScopeSpec(scopeName);
            var analytics = cluster.AnalyticsIndexes;
            await analytics.CreateDataverseAsync(dataverseName);
            string statement = "CREATE ANALYTICS COLLECTION `" + dataverseName + "`.`" + collectionName + "` ON `" + bucket.Name + "`.`" + scopeName + "`.`" + collectionName + "`";

            try
            {
                var analyticsResult = await cluster.AnalyticsQueryAsync<TestRequest>(statement).ConfigureAwait(false);
                var result = await analyticsResult.ToListAsync().ConfigureAwait(false);
                await collectionManager.CreateScopeAsync(scopeSpec).ConfigureAwait(false);
                var collectionSpec = new CollectionSpec(scopeName, collectionName);
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);
                var collectionExistsResult = await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                Assert.True(collectionExistsResult);
                var scope = bucket.Scope(scopeName);
                statement = "SELECT * FROM `" + collectionName + "` where `" + collectionName + "`.foo= \"bar\"";
                analyticsResult = await cluster.AnalyticsQueryAsync<TestRequest>(statement).ConfigureAwait(false);
                result = await analyticsResult.ToListAsync().ConfigureAwait(false);
                Assert.True(result.Any());

            }
            finally
            {
                // drop scope
                await collectionManager.DropScopeAsync(scopeName).ConfigureAwait(false);
            }
        }
    }
}
