using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Couchbase.Query.Couchbase.N1QL;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class DecrementTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public DecrementTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_decrement_value()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.DecrementAsync(key, options => options.Initial(1));
                Assert.Equal((ulong) 1, result.Content);

                // increment again, doc exists, increments to 2
                result = await collection.Binary.DecrementAsync(key);
                Assert.Equal((ulong) 0, result.Content);
            }
            finally
            {
                try
                {
                    await collection.RemoveAsync(key);
                } catch(Exception ex){}
            }
        }

        [Fact]
        public async Task Can_specify_initial_and_delta_values()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.DecrementAsync(key, options => options.Initial(5));
                Assert.Equal((ulong) 5, result.Content);

                // increment again, doc exists, increments to 6
                result = await collection.Binary.DecrementAsync(key, options => options.Delta(5));
                Assert.Equal((ulong) 0, result.Content);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Test_Expiry()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.DecrementAsync(key, options => options.Expiry(TimeSpan.FromMilliseconds(500)).Initial(1));
                Assert.True(result.Cas > 0);

                await Task.Delay(TimeSpan.FromMilliseconds(600));

                var notFound = await collection.ExistsAsync(key);
                Assert.True(notFound.Exists);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }
    }
}
