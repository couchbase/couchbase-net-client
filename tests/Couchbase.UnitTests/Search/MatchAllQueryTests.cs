using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class MatchAllQueryTests
    {
        [Fact]
        public void Export_Returns_Valid_Json()
        {
            var query = new MatchAllQuery();

            var expected = JsonConvert.SerializeObject(new
            {
                match_all = (string) null
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }
    }
}
