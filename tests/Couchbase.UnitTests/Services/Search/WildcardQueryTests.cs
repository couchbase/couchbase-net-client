using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class WildcardQueryTests
    {
        [Fact]
        public void Export_Returns_Valid_Json()
        {
            var query = new WildcardQuery("wildcard").Field("field");

            var expected = JsonConvert.SerializeObject(new
            {
                wildcard = "wildcard",
                field = "field"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new WildcardQuery("wildcard");

            var expected = JsonConvert.SerializeObject(new
            {
                wildcard = "wildcard"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }
    }
}
