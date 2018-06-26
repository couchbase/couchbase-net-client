using Couchbase.Search.Sort;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class IdSearchSortTests
    {
        [Test]
        public void Outputs_Valid_Json()
        {
            var sort = new IdSearchSort(true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "id",
                desc = true
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Omits_Decending_If_False()
        {
            var sort = new IdSearchSort();
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "id"
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }
    }
}
