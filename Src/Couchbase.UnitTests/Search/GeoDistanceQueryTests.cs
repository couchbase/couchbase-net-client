using System;
using Couchbase.Search.Queries.Geo;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class GeoDistanceQueryTests
    {
        [Test]
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

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
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

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Throws_InvalidOperationException_When_Required_Properties_Are_Missing()
        {
            Assert.Throws<InvalidOperationException>(() => new GeoDistanceQuery().Export());
            Assert.Throws<InvalidOperationException>(() => new GeoDistanceQuery().Longitude(1).Export());
            Assert.Throws<InvalidOperationException>(() => new GeoDistanceQuery().Longitude(1).Latitude(1).Export());
        }
    }
}
