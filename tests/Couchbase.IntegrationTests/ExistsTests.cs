using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.TestData;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class ExistsTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public ExistsTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Exists_returns_true_when_key_exists()
        {
            var key = Guid.NewGuid().ToString();
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(true);

            try
            {
                var result = await collection.ExistsAsync(key).ConfigureAwait(true);
                Assert.False(result.Exists);

                await collection.InsertAsync(key, new { }, options => options.Expiry(TimeSpan.FromHours(1))).ConfigureAwait(true);

                result = await collection.ExistsAsync(key).ConfigureAwait(true);
                Assert.True(result.Exists);
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(true);
            }
        }

        [Fact]
        public async Task Exists_returns_cas()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(true);
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new {}).ConfigureAwait(true);

                var get = await collection.GetAsync(key).ConfigureAwait(true);
                var result = await collection.ExistsAsync(key).ConfigureAwait(true);
                Assert.Equal(get.Cas, result.Cas);
                Assert.NotEqual(ulong.MinValue, result.Cas);
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(true);
            }
        }
    }
}
