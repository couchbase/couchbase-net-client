using Couchbase.Search;
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
            var query = new DocIdQuery("foo");
            var result = query.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                query = new
                {
                    boost = 0.0,
                    ids = new[]
                    {
                        "foo"
                    }
                }
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Export_With_SearchParams_ReturnsValidJson()
        {
            var query = new DocIdQuery("foo");

            var searchParams = new SearchParams();
            var result = query.Export(searchParams).ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                ctl = new
                {
                    timeout = 75000
                },
                query = new
                {
                    boost = 0.0,
                    ids = new[]
                    {
                        "foo"
                    }
                }
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }
    }
}
