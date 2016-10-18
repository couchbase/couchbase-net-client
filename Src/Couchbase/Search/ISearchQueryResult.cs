using System.Collections.Generic;

namespace Couchbase.Search
{
    /// <summary>
    /// The result of a search query.
    /// </summary>
    public interface ISearchQueryResult : IResult, IEnumerable<ISearchQueryRow>
    {
        /// <summary>
        /// The rows returned by the search request.
        /// </summary>
        IList<ISearchQueryRow> Hits { get; }

        /// <summary>
        /// The rows returned by the search request.
        /// Throws Exception if an execution error occured while processing requst.
        /// </summary>
        IList<ISearchQueryRow> HitsOrFail { get; }

        /// <summary>
        /// The facets for the result.
        /// </summary>
        IList<SearchFacet> Facets { get; }

        /// <summary>
        /// The errors returned from the server if the request failed.
        /// </summary>
        IList<string> Errors { get; }

        /// <summary>
        /// The status for the result.
        /// </summary>
        SearchStatus Status { get; }

        /// <summary>
        /// The metrics for the search. Includes number of hits, time taken, etc.
        /// </summary>
        SearchMetrics Metrics { get; }
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
