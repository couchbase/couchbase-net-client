using System;
using Couchbase.Core;
using OpenTracing;

namespace Couchbase.Views
{
    /// <summary>
    /// A base interface for View and Spatial query requests.
    /// </summary>
    public interface IViewQueryable
    {
        /// <summary>
        /// Gets the name of the <see cref="IBucket"/> that the query is targeting.
        /// </summary>
        string BucketName { get; }

        /// <summary>
        /// When true, the generated url will contain 'https' and use port 18092
        /// </summary>
        bool UseSsl { get; set; }

        /// <summary>
        /// The number of times the view request was retried if it fails before succeeding or giving up.
        /// </summary>
        /// <remarks>Used internally.</remarks>
        int RetryAttempts { get; set; }

        /// <summary>
        /// Returns the raw REST URI which can be executed in a browser or using curl.
        /// </summary>
        /// <returns>A <see cref="Uri"/> object that represents the query. This query can be run within a browser.</returns>
        Uri RawUri();

        /// <summary>
        /// Sets the base uri for the query if it's not set in the constructor.
        /// </summary>
        /// <param name="uri">The base uri to use - this is normally set internally and may be overridden by configuration.</param>
        /// <returns>An <see cref="IViewQueryable"/> object for chaining</returns>
        /// <remarks>Note that this will override the baseUri set in the ctor. Additionally, this method may be called internally by the <see cref="IBucket"/> and overridden.</remarks>
        IViewQueryable BaseUri(Uri uri);

        /// <summary>
        /// Toogles the if query result to is to be streamed. This is useful for large result sets in that it limits the
        /// working size of the query and helps reduce the possibility of a <see cref="OutOfMemoryException" /> from occurring.
        /// </summary>
        /// <param name="useStreaming">if set to <c>true</c> streams the results as you iterate through the response.</param>
        /// <returns>An IViewQueryable object for chaining</returns>
        IViewQueryable UseStreaming(bool useStreaming);

        /// <summary>
        /// Gets a value indicating if the result should be streamed.
        /// </summary>
        /// <value><c>true</c> if the query result is to be streamed; otherwise, <c>false</c>.</value>
        bool IsStreaming { get; }

        /// <summary>
        /// Builds a JSON string of the <see cref="IViewQueryable"/> used for posting the query to a Couchbase Server.
        /// </summary>
        string CreateRequestBody();

        /// <summary>
        /// The current active <see cref="ISpan"/> used for tracing.
        /// Intended for internal use only.
        /// </summary>
        ISpan ActiveSpan { get; set; }
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
