using System.Threading.Tasks;
using Couchbase.Analytics;
using Xunit;

namespace Couchbase.CombinationTests.Tests.Query
{
    [Collection(CombinationTestingCollection.Name)]
    public class AnalyticsTests
    {
        private readonly CouchbaseFixture _fixture;

        public AnalyticsTests(CouchbaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_Analytics_Basic()
        {
            var result = await _fixture.Cluster.AnalyticsQueryAsync<dynamic>("SELECT 1;");
            Assert.Equal(AnalyticsStatus.Success, result.MetaData.Status);
        }
    }
}
