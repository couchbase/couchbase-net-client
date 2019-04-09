using System;
using Couchbase.Services.Search.Queries.Geo;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class GeoDistanceQueryTests
    {
        [Fact]
        public void Export_ReturnsValidJson()
        {
            var query = new GeoDistanceQuery()
                .Longitude(1.5)
                .Latitude(2.0)
                .Distance("10mi")
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                location = new [] { 1.5, 2.0},
                distance = "10mi",
                field = "bar"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new GeoDistanceQuery()
                .Longitude(1.5)
                .Latitude(2.0)
                .Distance("10mi");

            var expected = JsonConvert.SerializeObject(new
            {
                location = new [] { 1.5, 2.0 },
                distance = "10mi"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Throws_InvalidOperationException_When_Required_Properties_Are_Missing()
        {
            Assert.Throws<InvalidOperationException>(() => new GeoDistanceQuery().Export());
            Assert.Throws<InvalidOperationException>(() => new GeoDistanceQuery().Longitude(1).Export());
            Assert.Throws<InvalidOperationException>(() => new GeoDistanceQuery().Longitude(1).Latitude(1).Export());
        }
    }
}
