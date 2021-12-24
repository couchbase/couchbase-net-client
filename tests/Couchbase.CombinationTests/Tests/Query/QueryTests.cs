using System.Threading.Tasks;
using Xunit;

namespace Couchbase.CombinationTests.Tests.Query
{
    [Collection(CombinationTestingCollection.Name)]
    public class QueryTests
    {
        private readonly CouchbaseFixture _fixture;

        public QueryTests(CouchbaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_Query_Basic()
        {
            var result = await _fixture.Cluster.QueryAsync<dynamic>("SELECT 1;");
            Assert.Empty(result.Errors);
        }
    }
}
