using System.Net;
using Couchbase.Services.Search;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class SearchQueryResultTests
    {
        [Fact]
        public void Should_return_true_if_status_code_is_429()
        {
            var result = new SearchResult
            {
                HttpStatusCode = (HttpStatusCode) 429
            };

            Assert.True(result.ShouldRetry());
        }
    }
}
