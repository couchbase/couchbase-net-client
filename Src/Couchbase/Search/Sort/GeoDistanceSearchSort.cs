using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Sort
{
    /// <summary>
    /// Sorts the search results by a field in the hits.
    /// </summary>
    public class GeoDistanceSearchSort : SearchSortBase
    {
        private double _longitude;
        private double _latitude;
        private string _field;
        private string _unit;

        public GeoDistanceSearchSort(double longitude, double latitude, string field, string unit = null, bool decending = false)
        {
            _longitude = longitude;
            _latitude = latitude;
            _field = field;
            _unit = unit;
            Decending = decending;
        }

        protected override string By
        {
            get { return "geo_distance"; }
        }

        public GeoDistanceSearchSort Unit(string unit)
        {
            _unit = unit;
            return this;
        }

        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("location", new JArray(_longitude, _latitude)));
            json.Add(new JProperty("field", _field));

            if (!string.IsNullOrWhiteSpace(_unit))
            {
                json.Add(new JProperty("unit", _unit));
            }

            return json;
        }
    }
}