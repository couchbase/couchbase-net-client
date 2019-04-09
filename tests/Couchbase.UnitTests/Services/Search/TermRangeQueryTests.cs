using System;
using Couchbase.Services.Search.Queries.Range;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class TermRangeQueryTests
    {
        [Fact]
        public void Export_ReturnsValidJson()
        {
            var query = new TermRangeQuery("test")
                .Min("lower", true)
                .Max("higher", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                term = "test",
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
            var query = new TermRangeQuery("test")
                .Max("higher", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                term = "test",
                max = "higher",
                inclusive_max = true,
                field = "bar"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Max_If_Not_Provided()
        {
            var query = new TermRangeQuery("test")
                .Min("lower", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                term = "test",
                min = "lower",
                inclusive_min = true,
                field = "bar"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new TermRangeQuery("test")
                .Min("lower", true)
                .Max("higher", true);

            var expected = JsonConvert.SerializeObject(new
            {
                term = "test",
                min = "lower",
                inclusive_min = true,
                max = "higher",
                inclusive_max = true
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Throws_InvalidOperatinException_When_Term_Is_Null_Or_Empty(string term)
        {
            Assert.Throws<ArgumentException>(() => new TermRangeQuery(term));
        }

        [Fact]
        public void Throws_InvalidOperatinException_When_Min_And_Max_Are_Not_Provided()
        {
            var query = new TermRangeQuery("test");
            Assert.Throws<InvalidOperationException>(() => query.Export());
        }
    }
}
