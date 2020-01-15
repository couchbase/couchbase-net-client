using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Query;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.IntegrationTests.Services.Analytics
{
    public class AnalyticsTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

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
        public void Execute_Query()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var result = _fixture.Cluster.AnalyticsQuery<TestRequest>(statement);

            Assert.Single(result.Rows);
            Assert.Equal("hello", result.Rows.First().Greeting);
        }

        [Fact]
        public async Task Execute_Query_Async()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var result = await _fixture.Cluster.AnalyticsQueryAsync<TestRequest>(statement).ConfigureAwait(false);

            Assert.Single(result.Rows);
            Assert.Equal("hello", result.Rows.First().Greeting);
        }

        [Fact]
        public async Task Test_Ingest()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var result = await _fixture.Cluster.IngestAsync<dynamic>(
                statement,
                await _fixture.GetDefaultCollection(),
                options =>
                {
                    options.WithTimeout(TimeSpan.FromSeconds(75));
                    options.WithExpiry(TimeSpan.FromDays(1));
                }
            ).ConfigureAwait(false);

            Assert.True(result.Any());
        }
    }
}
