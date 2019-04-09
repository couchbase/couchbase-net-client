using Couchbase.Services.Search.Queries.Simple;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class MatchNoneQueryTests
    {
        [Fact]
        public void Export_Returns_Valid_Json()
        {
            var query = new MatchNoneQuery();

            var expected = JsonConvert.SerializeObject(new
            {
                match_none = (string)null
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }
    }
}
