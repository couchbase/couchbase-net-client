using System;
using System.Collections.Generic;

namespace Couchbase.Search
{
    /// <summary>
    /// The result of a search query.
    /// </summary>
    public interface ISearchResult : IEnumerable<ISearchQueryRow>, IServiceResult
    {
        /// <summary>
        /// The rows returned by the search request.
        /// </summary>
        IList<ISearchQueryRow> Hits { get; }

        /// <summary>
        /// The results for the facet components of the query.
        /// </summary>
        IDictionary<string, IFacetResult> Facets { get; }

        MetaData MetaData { get; }
    }

    public class MetaData
    {
        public long SuccessCount { get; set; }

        public long ErrorCount { get; set; }

        public TimeSpan TimeTook { get; set; }

        public long TotalHits { get; set; }

        public double MaxScore { get; set; }

        public long TotalCount { get; set; }

        public Dictionary<string, string> Errors = new();
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
