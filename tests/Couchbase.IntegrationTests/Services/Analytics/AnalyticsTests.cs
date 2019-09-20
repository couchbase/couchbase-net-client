using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Services.Analytics;
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
        public void Can_execute_deferred_query()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var token = default(CancellationToken);
            Assert.Equal(CancellationToken.None, token);

            var result = _fixture.Cluster.AnalyticsQuery<TestRequest>(statement,
                options => { options.WithDeferred(true); }
            );

            Assert.Equal(QueryStatus.Running, result.MetaData.Status);

            Assert.NotNull(result.Handle);

            var status = result.Handle.GetStatus();
            Assert.Equal(QueryStatus.Success, status);

            var rows = result.Handle.GetRows();
            Assert.NotEmpty(rows);
            Assert.Equal("hello", rows.First().Greeting);
        }

        [Fact]
        public async Task Can_execute_deferred_query_async()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var result = await _fixture.Cluster.AnalyticsQueryAsync<TestRequest>(statement,
                options => { options.WithDeferred(true); }
            ).ConfigureAwait(false);

            Assert.True(result.MetaData.Status == QueryStatus.Success || result.MetaData.Status == QueryStatus.Running);

            Assert.NotNull(result.Handle);

            var status = await result.Handle.GetStatusAsync().ConfigureAwait(false);
            Assert.Equal(QueryStatus.Success, status);

            var rows = await result.Handle.GetRowsAsync().ConfigureAwait(false);
            Assert.NotEmpty(rows);
            Assert.Equal("hello", rows.First().Greeting);
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
