using Couchbase.Search.Sort;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class ScoreSearchSortTests
    {
        [Test]
        public void Outputs_Valid_Json()
        {
            var sort = new ScoreSearchSort(true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "score",
                desc = true
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Omits_Decending_If_False()
        {
            var sort = new ScoreSearchSort();
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "score"
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }
    }
}
