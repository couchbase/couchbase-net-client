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
        public async Task Execute_Query()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var cluster = await _fixture.GetCluster();
            var result = await cluster.AnalyticsQuery<TestRequest>(statement)
                .ToListAsync().ConfigureAwait(false);

            Assert.Single(result);
            Assert.Equal("hello", result.First().Greeting);
        }

        [Fact]
        public async Task Test_Ingest()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var cluster = await _fixture.GetCluster();
            var result = await cluster.IngestAsync<dynamic>(
                statement,
                await _fixture.GetDefaultCollection(),
                options =>
                {
                    options.Timeout(TimeSpan.FromSeconds(75));
                    options.Expiry(TimeSpan.FromDays(1));
                }
            ).ConfigureAwait(false);

            Assert.True(result.Any());
        }
    }
}
