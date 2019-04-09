using Couchbase.Services.Search.Sort;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class ScoreSearchSortTests
    {
        [Fact]
        public void Outputs_Valid_Json()
        {
            var sort = new ScoreSearchSort(true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "score",
                desc = true
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Omits_Decending_If_False()
        {
            var sort = new ScoreSearchSort();
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "score"
            }, Formatting.None);

            Assert.Equal(expected, result);
        }
    }
}
