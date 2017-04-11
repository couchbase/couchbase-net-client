using System;
using Couchbase.Search.Queries.Geo;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class GeoBoundingBoxQueryTests
    {
        [Test]
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

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
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

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Throws_InvalidOperationException_When_Required_Properties_Are_Missing()
        {
            Assert.Throws<InvalidOperationException>(() => new GeoBoundingBoxQuery().Export());
            Assert.Throws<InvalidOperationException>(() => new GeoBoundingBoxQuery().TopLeft(1, 2).Export());
            Assert.Throws<InvalidOperationException>(() => new GeoBoundingBoxQuery().BottomRight(1, 2).Export());
        }
    }
}
