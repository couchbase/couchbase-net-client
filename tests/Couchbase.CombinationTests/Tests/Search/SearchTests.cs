using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Search.Queries;
using Xunit;

namespace Couchbase.CombinationTests.Tests.Query
{
    [Collection(CombinationTestingCollection.Name)]
    public class SearchTests
    {
        private readonly CouchbaseFixture _fixture;

        public SearchTests(CouchbaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_Query_Basic()
        {
            //Just check for connectivity/service existence ATM
            await Assert.ThrowsAsync<IndexNotFoundException>(async () => await _fixture.Cluster.SearchQueryAsync("noop", new NoOpQuery()));
        }

        internal class NoOpQuery : SearchQueryBase
        {
            //no implementation
        }
    }
}
