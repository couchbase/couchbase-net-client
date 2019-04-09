using Couchbase.Services.Search.Sort;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class IdSearchSortTests
    {
        [Fact]
        public void Outputs_Valid_Json()
        {
            var sort = new IdSearchSort(true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "id",
                desc = true
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Omits_Decending_If_False()
        {
            var sort = new IdSearchSort();
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "id"
            }, Formatting.None);

            Assert.Equal(expected, result);
        }
    }
}
