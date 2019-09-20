using System;
using Couchbase.Services.Query;
using Newtonsoft.Json;

namespace Couchbase.Services.Search
{
    /// <summary>
    /// Represents a Full Text Search (FTS) query against an index for a given <see cref="IFtsQuery"/> implementation using <see cref="ISearchOptions"/>.
    /// </summary>
    public sealed class SearchQuery
    {
        public SearchQuery()
        {
            SearchOptions = new SearchOptions();
        }

        ///// <summary>
        ///// Gets or sets the credentials.
        ///// </summary>
        ///// <value>
        ///// The credentials.
        ///// </value>
        //internal IClusterCredentials Credentials { get; set; }

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
        /// Gets or sets the <see cref="ISearchOptions"/> parameters to pass to the FTS service.
        /// </summary>
        /// <value>
        /// The search parameters.
        /// </value>
        public ISearchOptions SearchOptions { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFtsQuery"/> to send to the FTS service.
        /// </summary>
        /// <value>
        /// The query.
        /// </value>
        public IFtsQuery Query { get; set; }

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

        internal Uri GetSearchUri()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Limits the number of matching results from a returned result-set.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public SearchQuery Limit(int limit)
        {
            SearchOptions.Limit(limit);
            return this;
        }

        /// <summary>
        /// Skip indicates how many matching results to skip on the result set before returing matches.
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>
        public SearchQuery Skip(int skip)
        {
            SearchOptions.Skip(skip);
            return this;
        }

        /// <summary>
        /// If true, the response will include additional search score explanations.
        /// </summary>
        /// <param name="explain"></param>
        /// <returns></returns>
        public SearchQuery Explain(bool explain)
        {
            SearchOptions.Explain(explain);
            return this;
        }

        /// <summary>
        /// Allows setting of additional highlighting on the result set of matching terms.
        /// </summary>
        /// <param name="highLightStyle">The <see cref="HighLightStyle" /> to use.</param>
        /// <returns></returns>
        public SearchQuery Highlighting(HighLightStyle highLightStyle)
        {
            SearchOptions.Highlighting(highLightStyle);
            return this;
        }

        /// <summary>
        /// Allows setting of additional highlighting on the result set of matching terms.
        /// </summary>
        /// <param name="highLightStyle">The <see cref="HighLightStyle" /> to use.</param>
        /// <param name="fields">The specific terms or fields to highlight.</param>
        /// <returns></returns>
        public SearchQuery Highlighting(HighLightStyle highLightStyle, params string[] fields)
        {
            SearchOptions.Highlighting(highLightStyle, fields);
            return this;
        }

        /// <summary>
        /// List of fields values that should be returned in the result assuming that they were indexed.
        /// </summary>
        /// <param name="fields">The indexed fields to return.</param>
        /// <returns></returns>
        public SearchQuery Fields(params string[] fields)
        {
            SearchOptions.Fields(fields);
            return this;
        }

        /// <summary>
        ///   <see cref="ISearchFacet" />s used to aggregate information collected on a particluar result set.
        /// </summary>
        /// <param name="searchFacets">The <see cref="ISearchFacet" /> to aggreate information on.</param>
        /// <returns></returns>
        public SearchQuery Facets(params ISearchFacet[] searchFacets)
        {
            SearchOptions.Facets(searchFacets);
            return this;
        }

        /// <summary>
        /// The server side timeout allows to specify an upper boundary of request execution so that it potentially doesn't run infinitely.
        /// </summary>
        /// <param name="timeout">The max length of time that that will be given to execute the query.</param>
        /// <returns></returns>
        public SearchQuery Timeout(TimeSpan timeout)
        {
            SearchOptions.Timeout(timeout);
            TimeoutValue = (uint) timeout.TotalMilliseconds * 1000; // convert from millis to micros
            return this;
        }

        /// <summary>
        /// The <see cref="ScanConsistency" /> you require for you <see cref="ISearchResult" />s.
        /// </summary>
        /// <param name="consistency">The <see cref="ScanConsistency" /> for documents to be included in the query results.</param>
        /// <returns></returns>
        public SearchQuery WithConsistency(ScanConsistency consistency)
        {
            SearchOptions.WithConsistency(consistency);
            return this;
        }

        /// <summary>
        /// Configures the list of fields which are used for sorting the search result. Fields with a prefix of "-" indicate a decending nature.
        /// If no sort is provided, it is equal to sort("-_score"), since the server will sort it by score in descending order by default.
        /// </summary>
        /// <param name="sort">The field names to sort by.</param>
        /// <returns></returns>
        public SearchQuery Sort(params string[] sort)
        {
            SearchOptions.Sort(sort);
            return this;
        }

        /// <summary>
        /// Gets the JSON representation of this object.
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            var json = SearchOptions.ToJson();
            if (Query != null)
            {
                json.Add("query", Query.Export());
            }

            return json.ToString(Formatting.None);
        }

        internal uint TimeoutValue { get; set; }
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
