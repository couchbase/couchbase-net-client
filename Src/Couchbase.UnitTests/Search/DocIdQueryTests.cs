using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class DocIdQueryTests
    {
        [Test]
        public void Export_ReturnsValidJson()
        {
            var query = new DocIdQuery("foo", "bar");

            var expected = JsonConvert.SerializeObject(new
            {
                ids = new[] {"foo", "bar"}
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }
    }
}
