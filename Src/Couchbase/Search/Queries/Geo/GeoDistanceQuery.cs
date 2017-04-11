using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Geo
{
    /// <summary>
    /// This query finds all matches from a given location (point) within the given distance.
    /// Both the point and the distance are required.
    /// </summary>
    public class GeoDistanceQuery : FtsQueryBase
    {
        private double? _longitude;
        private double? _latitude;
        private string _distance;
        private string _field;

        public GeoDistanceQuery Longitude(double longitude)
        {
            _longitude = longitude;
            return this;
        }

        public GeoDistanceQuery Latitude(double latitude)
        {
            _latitude = latitude;
            return this;
        }

        public GeoDistanceQuery Distance(string distance)
        {
            _distance = distance;
            return this;
        }

        public GeoDistanceQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export()
        {
            if (!_longitude.HasValue)
            {
                throw new InvalidOperationException("A GeoDistanceQuery must have a longitude specified");
            }
            if (!_latitude.HasValue)
            {
                throw new InvalidOperationException("A GeoDistanceQuery must have a latitude specified");
            }
            if (string.IsNullOrWhiteSpace(_distance))
            {
                throw new InvalidOperationException("A GeoDistanceQuery must have a distance specified");
            }

            var json = base.Export();
            json.Add(new JProperty("location", new JArray(new [] {_longitude, _latitude})));
            json.Add(new JProperty("distance", _distance));

            if (!string.IsNullOrWhiteSpace(_field))
            {
                json.Add(new JProperty("field", _field));
            }

            return json;
        }
    }
}
