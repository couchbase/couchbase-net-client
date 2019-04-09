using System;
using Couchbase.Services.Search.Sort;
using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search
{
    /// <summary>
    /// Represents the input options for a <see cref="IFtsQuery"/> request.
    /// </summary>
    public interface ISearchOptions
    {
        /// <summary>
        /// Limits the number of matching results from a returned result-set.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        ISearchOptions Limit(int limit);

        /// <summary>
        /// Skip indicates how many matching results to skip on the result set before returing matches.
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>
        ISearchOptions Skip(int skip);

        /// <summary>
        /// If true, the response will include additional search score explanations.
        /// </summary>
        /// <param name="explain"></param>
        /// <returns></returns>
        ISearchOptions Explain(bool explain);

        /// <summary>
        /// Allows setting of additional highlighting on the result set of matching terms.
        /// </summary>
        /// <param name="highLightStyle">The <see cref="HighLightStyle"/> to use.</param>
        /// <returns></returns>
        ISearchOptions Highlighting(HighLightStyle highLightStyle);

        /// <summary>
        /// Allows setting of additional highlighting on the result set of matching terms.
        /// </summary>
        /// <param name="highLightStyle">The <see cref="HighLightStyle"/> to use.</param>
        /// <param name="fields">The specific terms or fields to highlight.</param>
        /// <returns></returns>
        ISearchOptions Highlighting(HighLightStyle highLightStyle, params string[] fields);

        /// <summary>
        /// List of fields values that should be returned in the result assuming that they were indexed.
        /// </summary>
        /// <param name="fields">The indexed fields to return.</param>
        /// <returns></returns>
        ISearchOptions Fields(params string[] fields);

        /// <summary>
        /// <see cref="ISearchFacet"/>s used to aggregate information collected on a particluar result set.
        /// </summary>
        /// <param name="searchFacets">The <see cref="ISearchFacet"/> to aggreate information on.</param>
        /// <returns></returns>
        ISearchOptions Facets(params ISearchFacet[] searchFacets);

        /// <summary>
        /// The server side timeout allows to specify an upper boundary of request execution so that it potentially doesn't run infinitely.
        /// </summary>
        /// <param name="timeout">The max length of time that that will be given to execute the query.</param>
        /// <returns></returns>
        ISearchOptions Timeout(TimeSpan timeout);

        /// <summary>
        /// The <see cref="ScanConsistency"/> you require for you <see cref="ISearchResult"/>s.
        /// </summary>
        /// <param name="consistency">The <see cref="ScanConsistency"/> for documents to be included in the query results.</param>
        /// <returns></returns>
        ISearchOptions WithConsistency(ScanConsistency consistency);

        /// <summary>
        /// Configures the list of fields which are used for sorting the search result. Fields with a prefix of "-" indicate a decending nature.
        /// If no sort is provided, it is equal to sort("-_score"), since the server will sort it by score in descending order by default.
        /// </summary>
        /// <param name="sort">The field names to sort by.</param>
        /// <returns></returns>
        ISearchOptions Sort(params string[] sort);

        /// <summary>
        /// Configures the sorting criteria for the search results using an implementation of <see cref="ISearchSort"/>.
        /// </summary>
        /// <param name="sort">The sort.</param>
        /// <returns></returns>
        ISearchOptions Sort(ISearchSort sort);

        /// <summary>
        /// Configures the sorting criteria for the search results using a custom <see cref="JObject"/>.
        /// </summary>
        /// <param name="sort">The sort.</param>
        /// <returns></returns>
        ISearchOptions Sort(JObject sort);

        /// <summary>
        /// Gets the JSON representation of this object.
        /// </summary>
        /// <returns></returns>
        JObject ToJson();
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
