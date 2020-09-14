using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Couchbase.Core.Exceptions;
using Couchbase.Search.Queries.Geo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class GeopolyQueryTests
    {
        [Fact]
        public void Test_Export()
        {
            var jsonString =
                "{\"field\":\"geo\",\"polygon_points\":["+
                "{\"lat\":37.79393211306212,\"lon\":-122.44234633404847},"  +
                "{\"lat\":37.779958817339967,\"lon\":-122.43977141339417}," +
                "{\"lat\":37.788031092020155,\"lon\":-122.42925715405579}," +
                "{\"lat\":37.79026946582319,\"lon\":-122.41149020154114},"  +
                "{\"lat\":37.79571192027403,\"lon\":-122.40735054016113},"  +
                "{\"lat\":37.79393211306212,\"lon\":-122.44234633404847}]}";

            var expected = JsonConvert.DeserializeObject<JObject>(jsonString);

            var actual = new GeoPolygonQuery(new List<Coordinate>
            {
                Coordinate.OfLatLon(37.79393211306212, -122.44234633404847),
                Coordinate.OfLatLon(37.779958817339967, -122.43977141339417),
                Coordinate.OfLatLon(37.788031092020155, -122.42925715405579),
                Coordinate.OfLatLon(37.79026946582319, -122.41149020154114),
                Coordinate.OfLatLon(37.79571192027403, -122.40735054016113),
                Coordinate.OfLatLon(37.79393211306212, -122.44234633404847)
            }).Field("geo").Export();

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void When_Coordinates_Null_Throw_ArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GeoPolygonQuery(null));
        }

        [Fact]
        public void When_Coordinates_Empty_Throw_InvalidArgumentException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPolygonQuery(new List<Coordinate>()));
        }
    }
}
