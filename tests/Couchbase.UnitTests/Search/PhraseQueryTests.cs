using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class PhraseQueryTests
    {
        [Fact]
        public void Export_ReturnsValidJson()
        {
            var query = new PhraseQuery("foo").Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                terms = new[] {"foo"},
                field = "bar",
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new PhraseQuery("foo");

            var expected = JsonConvert.SerializeObject(new
            {
                terms = new[] {"foo"}
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }
    }
}
