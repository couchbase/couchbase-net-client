using System;
using System.Collections.Generic;
using Couchbase.Core;

namespace Couchbase.Search
{
    /// <summary>
    /// The result of a search query.
    /// </summary>
    public interface ISearchQueryResult : IResult, IEnumerable<ISearchQueryRow>
    {
        /// <summary>
        /// Yhe number of shards (pindex) of the FTS index that were successfully queried, returning hits.
        /// </summary>
        /// <returns></returns>
        long SuccessCount { get; }

        /// <summary>
        /// The count of errors.
        /// </summary>
        /// <returns></returns>
        long ErrorCount { get; }

        /// <summary>
        /// Gets the total count.
        /// </summary>
        /// <value>
        /// The total count.
        /// </value>
        long TotalCount { get; }

        /// <summary>
        /// Total time taken for the results.
        /// </summary>
        TimeSpan Took { get; }

        /// <summary>
        /// Total hits returned by the results.
        /// </summary>
        long TotalHits { get; }

        /// <summary>
        /// The maximum score within the results.
        /// </summary>
        double MaxScore { get; }

        /// <summary>
        /// The rows returned by the query request.
        /// </summary>
        IList<ISearchQueryRow> Hits { get; }

        /// <summary>
        /// The facets for the result.
        /// </summary>
        /// <value>
        /// The facets.
        /// </value>
        IList<SearchFacet> Facets { get; }

        /// <summary>
        /// The errors returned from the server if the request failed.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        IList<string> Errors { get; }

            /// <summary>
        /// Sets the lifespan of the search request; used to check if the request exceeded the maximum time
        /// configured for it in <see cref="ClientConfiguration.SearchRequestTimeout" />
        /// </summary>
        /// <value>
        /// The lifespan.
        /// </value>
        Lifespan Lifespan { get; set; }

        SearchStatus Status { get; set; }
    }

    #region [ License information ]

    /* ************************************************************
     *
     *    @author Couchbase <info@couchbase.com>
     *    @copyright 2014 Couchbase, Inc.
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
}
