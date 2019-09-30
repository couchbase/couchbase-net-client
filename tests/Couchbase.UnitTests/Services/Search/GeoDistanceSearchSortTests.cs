using Couchbase.Search.Sort;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class GeoDistanceSearchSortTests
    {
        [Fact]
        public void Outputs_Valid_Json()
        {
            var sort = new GeoDistanceSearchSort(0.1, -0.2, "foo", "mi", true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "geo_distance",
                desc = true,
                location = new [] { 0.1, -0.2},
                field = "foo",
                unit = "mi"
            }, Formatting.None);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Omits_Unit_If_Null_Or_Empty(string unit)
        {
            var sort = new GeoDistanceSearchSort(0.1, -0.2, "foo", unit, true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "geo_distance",
                desc = true,
                location = new[] { 0.1, -0.2 },
                field = "foo"
            }, Formatting.None);

            Assert.Equal(expected, result);
        }
    }
}
