using Couchbase.Services.Search.Queries.Simple;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class BooleanFieldQueryTests
    {
        [Fact]
        public void Export_Returns_Valid_Json()
        {
            var query = new BooleanFieldQuery(true)
                .Field("field");

            var expected = JsonConvert.SerializeObject(new
            {
                @bool = true,
                field = "field"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new BooleanFieldQuery(true);

            var expected = JsonConvert.SerializeObject(new
            {
                @bool = true
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }
    }
}
