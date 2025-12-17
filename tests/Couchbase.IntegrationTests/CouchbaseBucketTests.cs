using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class CouchbaseBucketTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public CouchbaseBucketTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_WaitUntilReadyAsync()
        {
            var bucket = await _fixture.GetDefaultBucket();
            await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
        }
    }
}
