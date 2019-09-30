using Newtonsoft.Json;

namespace Couchbase.Views
{
    public interface IViewRow<out T>
    {
        /// <summary>
        /// The identifier for the row
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The key emitted by the View Map function
        /// </summary>
        dynamic Key { get; }

        /// <summary>
        /// The value emitted by the View Map function or if a Reduce view, the value of the Reduce
        /// </summary>
        T Value { get; }

        /// <summary>
        /// If the View query was a <see cref="SpatialViewQuery"/> and the Map function emitted a geometry this field will contain the emitted geometry.
        /// </summary>
        /// <value>
        /// The geometry object optionally emitted from a GEO Spatial View. The structure must be compatible with the GEOJson specification.
        /// </value>
        /// <remarks>This value will be null for all non-Geo Views or if the geometry is not emitted from the Map function.</remarks>
        dynamic Geometry { get; }
    }

    /// <summary>
    /// Represents a single row returned from a View request
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ViewRow<T> : IViewRow<T>
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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
