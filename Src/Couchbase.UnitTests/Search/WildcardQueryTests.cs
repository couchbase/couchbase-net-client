using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class WildcardQueryTests
    {
        [Test]
        public void Export_Returns_Valid_Json()
        {
            var query = new WildcardQuery("wildcard").Field("field");

            var expected = JsonConvert.SerializeObject(new
            {
                wildcard = "wildcard",
                field = "field"
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new WildcardQuery("wildcard");

            var expected = JsonConvert.SerializeObject(new
            {
                wildcard = "wildcard"
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }
    }
}
