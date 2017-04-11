using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Geo
{
    /// <summary>
    /// This query finds all matches within a given box (identified by the upper left and lower right corner coordinates).
    /// Both coordinate points are required so the server can identify the right bounding box.
    /// </summary>
    public class GeoBoundingBoxQuery : FtsQueryBase
    {
        private double? _topLeftLongitude;
        private double? _topLeftLatitude;
        private double? _bottomRightLongitude;
        private double? _bottomRightLatitude;
        private string _field;

        public GeoBoundingBoxQuery TopLeft(double longitude, double latitude)
        {
            _topLeftLongitude = longitude;
            _topLeftLatitude = latitude;
            return this;
        }

        public GeoBoundingBoxQuery BottomRight(double longitude, double latitude)
        {
            _bottomRightLongitude = longitude;
            _bottomRightLatitude = latitude;
            return this;
        }

        public GeoBoundingBoxQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export()
        {
            if (!_topLeftLongitude.HasValue ||
                !_topLeftLatitude.HasValue ||
                !_bottomRightLongitude.HasValue ||
                !_bottomRightLatitude.HasValue)
            {
                throw new InvalidOperationException();
            }

            var json = base.Export();
            json.Add("top_left", new JArray(new[] {_topLeftLongitude, _topLeftLatitude}));
            json.Add("bottom_right", new JArray(new[] {_bottomRightLongitude, _bottomRightLatitude}));

            if (!string.IsNullOrWhiteSpace(_field))
            {
                json.Add("field", _field);
            }

            return json;
        }
    }
}
