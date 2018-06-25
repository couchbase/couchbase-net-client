using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class BooleanFieldQueryTests
    {
        [Test]
        public void Export_Returns_Valid_Json()
        {
            var query = new BooleanFieldQuery(true)
                .Field("field");

            var expected = JsonConvert.SerializeObject(new
            {
                @bool = true,
                field = "field"
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new BooleanFieldQuery(true);

            var expected = JsonConvert.SerializeObject(new
            {
                @bool = true
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }
    }
}
