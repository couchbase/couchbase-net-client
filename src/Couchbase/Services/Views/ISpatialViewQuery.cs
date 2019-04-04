using System.Collections.Generic;

namespace Couchbase.Services.Views
{
    /// <summary>
    /// An interface for Spatial view request which provide multidimensional spatial indexes in Couchbase.
    /// </summary>
    internal interface ISpatialViewQuery : IViewQueryable
    {
        /// <summary>
        /// The start range of the spatial query.
        /// </summary>
        /// <param name="startRange">The start range.</param>
        /// <remarks>The number of elements must match the number of dimensions of the index</remarks>
        /// <remarks>Array of numeric or null; optional</remarks>
        /// <returns></returns>
        ISpatialViewQuery StartRange(List<double?> startRange);

        /// <summary>
        /// The start range of the spatial query.
        /// </summary>
        /// <param name="startRange">The start range.</param>
        /// <remarks>The number of elements must match the number of dimensions of the index</remarks>
        /// <remarks>Array of numeric or null; optional</remarks>
        /// <returns></returns>
        ISpatialViewQuery StartRange(params double?[] startRange);

        /// <summary>
        /// The end range of the spatial query.
        /// </summary>
        /// <param name="endRange">The end range.</param>
        /// <remarks>The number of elements must match the number of dimensions of the index</remarks>
        /// <remarks>Array of numeric or null; optional</remarks>
        /// <returns></returns>
        ISpatialViewQuery EndRange(List<double?> endRange);

        /// <summary>
        /// The end range of the spatial query.
        /// </summary>
        /// <param name="endRange">The end range.</param>
        /// <remarks>The number of elements must match the number of dimensions of the index</remarks>
        /// <remarks>Array of numeric or null; optional</remarks>
        /// <returns></returns>
        ISpatialViewQuery EndRange(params double?[] endRange);

        /// <summary>
        /// The start and end range for a spatial query.
        /// </summary>
        /// <param name="startRange">The start range.</param>
        /// <param name="endRange">The end range.</param>
        /// <returns></returns>
        ISpatialViewQuery Range(List<double?> startRange, List<double?> endRange);

        /// <summary>
        /// Skip this number of records before starting to return the results
        /// </summary>
        /// <param name="count">The number of records to skip</param>
        /// <returns></returns>
        ISpatialViewQuery Skip(int count);

        /// <summary>
        /// Allow the results from a stale view to be used. The default is StaleState.Ok; for development work set to StaleState.False
        /// </summary>
        /// <param name="staleState">The staleState value to use.</param>
        /// <returns>An ISpatialViewQuery object for chaining</returns>
        ISpatialViewQuery Stale(StaleState staleState);

        /// <summary>
        /// Limit the number of the returned documents to the specified number
        /// </summary>
        /// <param name="limit">The numeric limit</param>
        /// <returns>An ISpatialViewQuery object for chaining</returns>
        ISpatialViewQuery Limit(int limit);


        /// <summary>
        /// Sets the name of the Couchbase Bucket.
        /// </summary>
        /// <param name="name">The name of the bucket.</param>
        /// <returns>An ISpatialViewQuery object for chaining</returns>
        ISpatialViewQuery Bucket(string name);

        /// <summary>
        /// Toggles the query between development or production dataset and View.
        /// </summary>
        /// <param name="development">If true the development View will be used</param>
        /// <returns>An ISpatialViewQuery object for chaining</returns>
        ISpatialViewQuery Development(bool development);

        /// <summary>
        /// Gets the hostname or IP of the remote Couchbase server which will execute the query.
        /// </summary>
        /// <value>
        /// The host.
        /// </value>
        string Host { get; }

        /// <summary>
        /// Specifies the design document and view to execute.
        /// </summary>
        /// <param name="designDoc">The design document.</param>
        /// <param name="view">The view.</param>
        /// <returns></returns>
        ISpatialViewQuery From(string designDoc, string view);

        /// <summary>
        /// Specifies the design document.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        ISpatialViewQuery DesignDoc(string name);

        /// <summary>
        /// Specifies the view to execute.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        ISpatialViewQuery View(string name);

        /// <summary>
        /// Specifies the server timeout.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns></returns>
        ISpatialViewQuery ConnectionTimeout(int timeout);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
