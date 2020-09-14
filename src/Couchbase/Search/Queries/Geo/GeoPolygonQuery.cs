using System;
using System.Collections.Generic;
using Couchbase.Core.Exceptions;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Geo
{
    /// <summary>
    /// Performs a random bounding polygon query to select documents within that polygon area.
    /// </summary>
    /// <remarks>This class is Uncommitted and may change in future versions.</remarks>
    public class GeoPolygonQuery : SearchQueryBase
    {
        private readonly List<Coordinate> _coordinates;
        private string _field;

        /// <summary>
        /// Creates a GeoPolygonQuery given a list of <see cref="Coordinate"/>.
        /// </summary>
        /// <param name="coordinates"></param>
        public GeoPolygonQuery([NotNull] List<Coordinate> coordinates)
        {
            _coordinates = coordinates ?? throw new ArgumentNullException(nameof(coordinates));
            if (_coordinates.Count == 0) throw new ArgumentOutOfRangeException(nameof(coordinates), "One or more Coordinates must be provided.");
        }

        /// <summary>
        /// The optional field to search.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public GeoPolygonQuery Field(string field)
        {
            _field = field;
            return this;
        }

        /// <summary>
        /// Exports the GeoPolygonQuery as a JSON object.
        /// </summary>
        /// <returns></returns>
        public override JObject Export()
        {
            var json = base.Export();
            if (!string.IsNullOrWhiteSpace(_field))
            {
                json.Add("field", _field);
            }

            var points = new JArray();
            foreach (var coordinate in _coordinates)
            {
                points.Add(new JObject
                {
                    {"lat", coordinate.Lat},
                    {"lon",  coordinate.Lon}
                });
            }

            json.Add("polygon_points", points);

            return json;
        }
    }
}
