using System.Net;
using Couchbase.Search;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class SearchQueryResultTests
    {
        [Test]
        public void Should_return_true_if_status_code_is_429()
        {
            var result = new SearchQueryResult
            {
                HttpStatusCode = (HttpStatusCode) 429
            };

            Assert.IsTrue(result.ShouldRetry());
        }
    }
}
