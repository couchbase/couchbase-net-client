using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Extensions.OpenTelemetry.IntegrationTests;
using Couchbase.Test.Common;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.Extensions.Tracing.Otel.IntegrationTests
{
    [Collection(NonParallelDefinition.Name)]
    public class InMemoryTracerTests : IClassFixture<InMemoryTracingFixture>
    {
        private readonly InMemoryTracingFixture _fixture;

        public InMemoryTracerTests(InMemoryTracingFixture fixture)
        {
            _fixture = fixture;
        }

        //Collection of basic tests that checks for the right Otel Tags

        [Fact]
        public async Task Key_value_CRUD()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new { name = "mike" });
                Assert.Equal("insert", _fixture.exportedItems.Last().DisplayName);

                await collection.UpsertAsync(key, new { name = "john" });
                Assert.Equal("upsert", _fixture.exportedItems.Last().DisplayName);

                using (var result = await collection.GetAsync(key))
                {
                    var content = result.ContentAs<dynamic>();

                    Assert.Equal("john", (string)content.name);
                    Assert.Equal("get", _fixture.exportedItems.Last().DisplayName);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
                Assert.Equal("remove", _fixture.exportedItems.Last().DisplayName);

            }
        }

        [Fact]
        public async Task Key_value_withcompression()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new { name = "mike mike mike mike mike mike mike mike mike mike mike mike mike mike mike" });
                Assert.Equal("insert", _fixture.exportedItems.Last().DisplayName);

                await collection.UpsertAsync(key, new { name = "john john john john john john john john john john john john john john john" });
                Assert.Equal("upsert", _fixture.exportedItems.Last().DisplayName);

                using (var result = await collection.GetAsync(key))
                {
                    var content = result.ContentAs<dynamic>();

                    Assert.Equal("john john john john john john john john john john john john john john john", (string)content.name);
                    Assert.Contains("request_compression", _fixture.exportedItems.Select(p => p.DisplayName));
                    Assert.Contains("response_decompression", _fixture.exportedItems.Select(p => p.DisplayName));
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
                Assert.Equal("remove", _fixture.exportedItems.Last().DisplayName);

            }
        }

        [Fact]
        public async Task Key_value_increment_decrement()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.IncrementAsync(key,
                    new KeyValue.IncrementOptions().Initial(1));
                Assert.Equal((ulong)1, result.Content);
                Assert.Equal("increment", _fixture.exportedItems.Last().DisplayName);

                // increment again, doc exists, increments to 2
                result = await collection.Binary.IncrementAsync(key);
                Assert.Equal((ulong)2, result.Content);
                Assert.Equal("increment", _fixture.exportedItems.Last().DisplayName);

                // decrement, doc exists, decrement by 2
                result = await collection.Binary.DecrementAsync(key);
                Assert.Equal((ulong)1, result.Content);
                Assert.Equal("decrement", _fixture.exportedItems.Last().DisplayName);

            }
            finally
            {
                try
                {
                    await collection.RemoveAsync(key);
                }
                catch (DocumentNotFoundException)
                {
                }
            }
        }

        [Fact]
        public async Task Test_Query()
        {
            var cluster = await _fixture.GetCluster();
            await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` LIMIT 1;");
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

            var cluster = await _fixture.GetCluster();
            var analyticsResult = await cluster.AnalyticsQueryAsync<TestRequest>(statement);
            var result = await analyticsResult.ToListAsync();

            Assert.Single(result);
            Console.WriteLine(result.ToString());
            Assert.Equal("hello", result.First().Greeting);
            Assert.Equal("analytics", _fixture.exportedItems.Last().DisplayName);
        }

    }


}
