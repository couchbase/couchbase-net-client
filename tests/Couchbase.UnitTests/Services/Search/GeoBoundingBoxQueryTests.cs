using System;
using Couchbase.Services.Search.Queries.Geo;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class GeoBoundingBoxQueryTests
    {
        [Fact]
        public void Export_ReturnsValidJson()
        {
            var query = new GeoBoundingBoxQuery()
                .TopLeft(1, 2)
                .BottomRight(3, 4)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                top_left = new[] {1.0, 2.0},
                bottom_right = new[] {3.0, 4.0},
                field = "bar"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new GeoBoundingBoxQuery()
                .TopLeft(1, 2)
                .BottomRight(3, 4);

            var expected = JsonConvert.SerializeObject(new
            {
                top_left = new[] { 1.0, 2.0 },
                bottom_right = new[] { 3.0, 4.0 }
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Throws_InvalidOperationException_When_Required_Properties_Are_Missing()
        {
            Assert.Throws<InvalidOperationException>(() => new GeoBoundingBoxQuery().Export());
            Assert.Throws<InvalidOperationException>(() => new GeoBoundingBoxQuery().TopLeft(1, 2).Export());
            Assert.Throws<InvalidOperationException>(() => new GeoBoundingBoxQuery().BottomRight(1, 2).Export());
        }
    }
}
