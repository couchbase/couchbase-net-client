using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search
{
    /// <summary>
    /// Represents a Full Text Search (FTS) query against an index for a given <see cref="IFtsQuery"/> implementation using <see cref="ISearchParams"/>.
    /// </summary>
    public sealed class SearchQuery
    {
        /// <summary>
        /// The virtual path template for the API
        /// </summary>
        private static string ApiPath = "/api/index/{0}/query";

        /// <summary>
        /// Gets or sets the index to use for the FTS query.
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        public string Index { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ISearchParams"/> parameters to pass to the FTS service.
        /// </summary>
        /// <value>
        /// The search parameters.
        /// </value>
        public ISearchParams SearchParams { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFtsQuery"/> to send to the FTS service.
        /// </summary>
        /// <value>
        /// The query.
        /// </value>
        public IFtsQuery Query { get; set; }

        /// <summary>
        /// Exports this instance as a JSON object, which is used as the FTS request body.
        /// </summary>
        /// <returns></returns>
        internal JObject Export()
        {
            return Query.Export(SearchParams);
        }

        /// <summary>
        /// Gets the relative path for the FTS API service.
        /// </summary>
        /// <returns></returns>
        internal string RelativeUri()
        {
            if (string.IsNullOrWhiteSpace(Index))
            {
                throw new InvalidOperationException("The index name must be provided.");
            }
            return string.Format(ApiPath, Index);
        }
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
