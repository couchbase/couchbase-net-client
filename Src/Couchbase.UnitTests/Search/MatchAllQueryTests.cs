using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class MatchAllQueryTests
    {
        [Test]
        public void Export_Returns_Valid_Json()
        {
            var query = new MatchAllQuery();

            var expected = JsonConvert.SerializeObject(new
            {
                match_all = (string) null
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }
    }
}
