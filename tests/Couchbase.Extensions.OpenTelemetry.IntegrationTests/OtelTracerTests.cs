using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Extensions.OpenTelemetry.IntegrationTests;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.Extensions.Tracing.Otel.IntegrationTests
{
    public class OtelTracerTests : IClassFixture<OtelClusterFixture>
    {
        private readonly OtelClusterFixture _fixture;

        public OtelTracerTests(OtelClusterFixture fixture)
        {
            _fixture = fixture;

        }

        //Collection of basic tests that checks for the right Otel Tags

        [Fact]
        public async Task Key_value_CRUD()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new { name = "mike" }).ConfigureAwait(false);
                Assert.Equal("insert", _fixture.exportedItems.Last().DisplayName);

                await collection.UpsertAsync(key, new { name = "john" }).ConfigureAwait(false);
                Assert.Equal("upsert", _fixture.exportedItems.Last().DisplayName);

                using (var result = await collection.GetAsync(key).ConfigureAwait(false))
                {
                    var content = result.ContentAs<dynamic>();

                    Assert.Equal("john", (string)content.name);
                    Assert.Equal("get", _fixture.exportedItems.Last().DisplayName);
                }
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(false);
                Assert.Equal("remove", _fixture.exportedItems.Last().DisplayName);

            }
        }


        [Fact]
        public async Task Key_value_increment_decrement()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.IncrementAsync(key).ConfigureAwait(true);
                Assert.Equal((ulong)1, result.Content);
                Assert.Equal("increment", _fixture.exportedItems.Last().DisplayName);

                // increment again, doc exists, increments to 2
                result = await collection.Binary.IncrementAsync(key).ConfigureAwait(false);
                Assert.Equal((ulong)2, result.Content);
                Assert.Equal("increment", _fixture.exportedItems.Last().DisplayName);

                // decrement, doc exists, decrement by 2
                result = await collection.Binary.DecrementAsync(key).ConfigureAwait(false);
                Assert.Equal((ulong)1, result.Content);
                Assert.Equal("decrement", _fixture.exportedItems.Last().DisplayName);

            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Test_Query()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` LIMIT 1;").ConfigureAwait(false);
            Assert.Equal("query", _fixture.exportedItems.Last().DisplayName);
        }

        private class TestRequest
        {
            [JsonProperty("greeting")]
            public string Greeting { get; set; }
        }

        [Fact]
        public async Task Test_Analytics()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var analyticsResult = await cluster.AnalyticsQueryAsync<TestRequest>(statement).ConfigureAwait(false);
            var result = await analyticsResult.ToListAsync();

            Assert.Single(result);
            Console.WriteLine(result.ToString());
            Assert.Equal("hello", result.First().Greeting);
            Assert.Equal("analytics", _fixture.exportedItems.Last().DisplayName);
        }

    }


}
