using Couchbase.Services.Search.Queries.Simple;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class DocIdQueryTests
    {
        [Fact]
        public void Export_ReturnsValidJson()
        {
            var query = new DocIdQuery("foo", "bar");

            var expected = JsonConvert.SerializeObject(new
            {
                ids = new[] {"foo", "bar"}
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }
    }
}
