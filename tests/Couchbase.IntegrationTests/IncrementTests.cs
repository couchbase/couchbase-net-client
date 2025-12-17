using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class IncrementTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public IncrementTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_increment_value()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(true);
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.IncrementAsync(key, options =>
                    options.Initial(1)).ConfigureAwait(true);
                Assert.Equal((ulong) 1, result.Content);

                // increment again, doc exists, increments to 2
                result = await collection.Binary.IncrementAsync(key).ConfigureAwait(true);
                Assert.Equal((ulong) 2, result.Content);
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(true);
            }
        }

        [Fact]
        public async Task Can_specify_initial_and_delta_values()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(true);
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.IncrementAsync(key, options => options.Initial(5)).ConfigureAwait(true);
                Assert.Equal((ulong) 5, result.Content);

                // increment again, doc exists, increments to 6
                result = await collection.Binary.IncrementAsync(key, options => options.Delta(5)).ConfigureAwait(true);
                Assert.Equal((ulong) 10, result.Content);
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(true);
            }
        }

        [Fact]
        public async Task Test_Expiry()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(true);
            var key = Guid.NewGuid().ToString();

            try
            {
                // doc doesn't exist, create it and use initial value (1)
                var result = await collection.Binary.IncrementAsync(key, options => options.Expiry(TimeSpan.FromMilliseconds(500)).Initial(1)).ConfigureAwait(true);
                Assert.True(result.Cas > 0);

                await Task.Delay(TimeSpan.FromMilliseconds(600)).ConfigureAwait(true);

                var notFound = await collection.ExistsAsync(key).ConfigureAwait(true);
                Assert.True(notFound.Exists);
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(true);
            }
        }
    }
}
