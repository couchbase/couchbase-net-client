using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class MatchNoneQueryTests
    {
        [Test]
        public void Export_Returns_Valid_Json()
        {
            var query = new MatchNoneQuery();

            var expected = JsonConvert.SerializeObject(new
            {
                match_none = (string)null
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }
    }
}