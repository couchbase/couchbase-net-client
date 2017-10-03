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
        public string Id { get; set; }

        /// <summary>
        /// The key emitted by the View Map function
        /// </summary>
        public dynamic Key { get; set; }

        /// <summary>
        /// The value emitted by the View Map function or if a Reduce view, the value of the Reduce
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// If the View query was a <see cref="SpatialViewQuery"/> and the Map function emitted a geometry this field will contain the emitted geometry.
        /// </summary>
        /// <value>
        /// The geometry object optionally emited from a GEO Spatial View. The structure must be compatible with the GEOJson specification.
        /// </value>
        /// <remarks>This value will be null for all non-Geo Views or if the geometry is not emitted from the Map function.</remarks>
        public dynamic Geometry { get; set; }
    }

    internal class ViewRowData<T>
    {
        public string id { get; set; }

        public dynamic key { get; set; }

        public T value { get; set; }

        public dynamic geometry { get; set; }

        internal ViewRow<T> ToViewRow()
        {
            return new ViewRow<T>
            {
                Id = id,
                Key = key,
                Value = value,
                Geometry = geometry,
            };
        }
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
