using System;
using Couchbase.Search.Queries.Range;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class TermRangeQueryTests
    {
        [Fact]
        public void Export_ReturnsValidJson()
        {
            var query = new TermRangeQuery()
                .Min("lower", true)
                .Max("higher", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                min = "lower",
                inclusive_min = true,
                max = "higher",
                inclusive_max = true,
                field = "bar"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Min_If_Not_Provided()
        {
            var query = new TermRangeQuery()
                .Max("higher", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                max = "higher",
                inclusive_max = true,
                field = "bar"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Max_If_Not_Provided()
        {
            var query = new TermRangeQuery()
                .Min("lower", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                min = "lower",
                inclusive_min = true,
                field = "bar"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new TermRangeQuery()
                .Min("lower", true)
                .Max("higher", true);

            var expected = JsonConvert.SerializeObject(new
            {
                min = "lower",
                inclusive_min = true,
                max = "higher",
                inclusive_max = true
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }


        [Fact]
        public void Throws_InvalidOperatinException_When_Min_And_Max_Are_Not_Provided()
        {
            var query = new TermRangeQuery();
            Assert.Throws<InvalidOperationException>(() => query.Export());
        }
    }
}
