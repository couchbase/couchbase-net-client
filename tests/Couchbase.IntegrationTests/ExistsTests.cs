using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.TestData;
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
            var collection = await _fixture.GetDefaultCollection();

            try
            {
                var result = await collection.ExistsAsync(key);
                Assert.False(result.Exists);

                await collection.InsertAsync(key, new { });

                result = await collection.ExistsAsync(key);
                Assert.True(result.Exists);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Exists_returns_cas()
        {
            var collection = await _fixture.GetDefaultCollection();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new {});

                var get = await collection.GetAsync(key);
                var result = await collection.ExistsAsync(key);
                Assert.Equal(get.Cas, result.Cas);
                Assert.NotEqual(ulong.MinValue, result.Cas);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }
    }
}
