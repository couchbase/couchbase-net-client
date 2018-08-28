using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class PhraseQueryTests
    {
        [Test]
        public void Export_ReturnsValidJson()
        {
            var query = new PhraseQuery("foo").Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                terms = new[] {"foo"},
                field = "bar",
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new PhraseQuery("foo");

            var expected = JsonConvert.SerializeObject(new
            {
                terms = new[] {"foo"}
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }
    }
}
