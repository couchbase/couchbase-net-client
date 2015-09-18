using Newtonsoft.Json;

namespace Couchbase.Views
{
    /// <summary>
    /// Represents a single row returned from a View request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ViewRow<T>
    {
        /// <summary>
        /// The identifier for the row
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// The key emitted by the View Map function
        /// </summary>
        [JsonProperty("key")]
        public dynamic Key { get; set; }

        /// <summary>
        /// The value emitted by the View Map function or if a Reduce view, the value of the Reduce
        /// </summary>
        [JsonProperty("value")]
        public T Value { get; set; }

        /// <summary>
        /// If the View query was a <see cref="SpatialViewQuery"/> and the Map function emitted a geometry this field will contain the emitted geometry.
        /// </summary>
        /// <value>
        /// The geometry object optionally emited from a GEO Spatial View. The structure must be compatible with the GEOJson specification.
        /// </value>
        /// <remarks>This value will be null for all non-Geo Views or if the geometry is not emitted from the Map function.</remarks>
        [JsonProperty("geometry")]
        public dynamic Geometry { get; set; }
    }
}
