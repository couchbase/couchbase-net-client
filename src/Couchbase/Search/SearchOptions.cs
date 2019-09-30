using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Query;
using Couchbase.Search.Sort;
using Couchbase.Utils;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search
{
    /// <summary>
    /// Represents a number of query options that can be applied to a FTS query request.
    /// </summary>
    /// <seealso cref="ISearchOptions" />
    public class SearchOptions : ISearchOptions
    {
        private int? _limit;
        private int? _skip;
        private bool? _explain;
        private string  _highLightStyle;
        private readonly List<string> _fields = new List<string>();
        private List<string> _highLightFields;
        private List<ISearchFacet> _facets;
        private TimeSpan _timeOut = new TimeSpan(0, 0, 0, 0, 75000);
        private ScanConsistency?  _scanConsistency;
        private readonly JArray _sort = new JArray();

        /// <summary>
        /// Limits the number of matching results from a returned result-set.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public ISearchOptions Limit(int limit)
        {
            _limit = limit;
            return this;
        }

        /// <summary>
        /// Skip indicates how many matching results to skip on the result set before returing matches.
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>
        public ISearchOptions Skip(int skip)
        {
            _skip = skip;
            return this;
        }

        /// <summary>
        /// If true, the response will include additional search score explanations.
        /// </summary>
        /// <param name="explain"></param>
        /// <returns></returns>
        public ISearchOptions Explain(bool explain)
        {
            _explain = explain;
            return this;
        }

        /// <summary>
        /// Allows setting of additional highlighting on the result set of matching terms.
        /// </summary>
        /// <param name="highLightStyle">The <see cref="HighLightStyle" /> to use.</param>
        /// <returns></returns>
        public ISearchOptions Highlighting(HighLightStyle highLightStyle)
        {
            var name = Enum.GetName(typeof (HighLightStyle), highLightStyle);
            if (name != null)
            {
                _highLightStyle = name.ToLowerInvariant();
            }
            return this;
        }

        /// <summary>
        /// Allows setting of additional highlighting on the result set of matching terms.
        /// </summary>
        /// <param name="highLightStyle">The <see cref="HighLightStyle" /> to use.</param>
        /// <param name="fields">The specific terms or fields to highlight.</param>
        /// <returns></returns>
        public ISearchOptions Highlighting(HighLightStyle highLightStyle, params string[] fields)
        {
            var name = Enum.GetName(typeof(HighLightStyle), highLightStyle);
            if (name != null)
            {
                _highLightStyle = name.ToLowerInvariant();
            }
            if (_highLightFields == null)
            {
                _highLightFields = new List<string>();
            }
            _highLightFields.AddRange(fields);
            return this;
        }

        /// <summary>
        /// List of fields values that should be returned in the result assuming that they were indexed.
        /// </summary>
        /// <param name="fields">The indexed fields to return.</param>
        /// <returns></returns>
        public ISearchOptions Fields(params string[] fields)
        {
            if(fields == null || fields.Length <= 0)
            {
                throw new ArgumentNullException("fields", "must be non-null and have at least one value.");
            }
            //if fields are explicitly provided remove default wildcard
            if (_fields.Contains("*"))
            {
                _fields.Remove("*");
            }
            _fields.AddRange(fields);
            return this;
        }

        /// <summary>
        ///   <see cref="ISearchFacet" />s used to aggregate information collected on a particluar result set.
        /// </summary>
        /// <param name="searchFacets">The <see cref="ISearchFacet" /> to aggreate information on.</param>
        /// <returns></returns>
        public ISearchOptions Facets(params ISearchFacet[] searchFacets)
        {
            if (_facets == null)
            {
                _facets = new List<ISearchFacet>();
            }
            _facets.AddRange(searchFacets);
            return this;
        }

        /// <summary>
        /// The server side timeout allows to specify an upper boundary of request execution so that it potentially doesn't run infinitely.
        /// </summary>
        /// <param name="timeout">The max length of time that that will be given to execute the query.</param>
        /// <returns></returns>
        public ISearchOptions Timeout(TimeSpan timeout)
        {
            _timeOut = timeout;
            return this;
        }

        /// <summary>
        /// The <see cref="ScanConsistency" /> you require for you <see cref="ISearchResult" />s.
        /// </summary>
        /// <param name="consistency">The <see cref="ScanConsistency" /> for documents to be included in the query results.</param>
        /// <returns></returns>
        public ISearchOptions WithConsistency(ScanConsistency consistency)
        {
            _scanConsistency = consistency;
            return this;
        }

        /// <summary>
        /// Configures the list of fields which are used for sorting the search result. Fields with a prefix of "-" indicate a decending nature.
        /// If no sort is provided, it is equal to sort("-_score"), since the server will sort it by score in descending order by default.
        /// </summary>
        /// <param name="sort">The field names to sort by.</param>
        /// <returns></returns>
        public ISearchOptions Sort(params string[] sort)
        {
            if (sort != null)
            {
                _sort.Add(sort);
            }
            return this;
        }

        /// <summary>
        /// Configures the sorting criteria for the search results using an implementation of <see cref="ISearchSort" />.
        /// </summary>
        /// <param name="sort">The sort.</param>
        /// <returns></returns>
        public ISearchOptions Sort(ISearchSort sort)
        {
            if (sort != null)
            {
                _sort.Add(sort.Export());
            }
            return this;
        }

        /// <summary>
        /// Configures the sorting criteria for the search results using a custom <see cref="JObject" />.
        /// </summary>
        /// <param name="sort">The sort.</param>
        /// <returns></returns>
        public ISearchOptions Sort(JObject sort)
        {
            if (sort != null)
            {
                _sort.Add(sort);
            }
            return this;
        }

        public JObject ToJson()
        {
            var ctl = new JObject(new JProperty("timeout", (long) _timeOut.TotalMilliseconds));
            if (_scanConsistency.HasValue)
            {
                var consistency = new JObject(
                        new JProperty("level", _scanConsistency.GetDescription()));

#pragma warning disable 618
                if (_scanConsistency == ScanConsistency.AtPlus)//needs resolution
#pragma warning restore 618
                {
                    consistency.Add(new JProperty("vectors", new JObject()));//does nothing ATM!
                }
                ctl.Add("consistency", consistency);
            }

            var parameters = new JObject(new JProperty("ctl", ctl));
            if (_limit.HasValue)
            {
                parameters.Add(new JProperty("size", _limit));
            }
            if (_skip.HasValue)
            {
                parameters.Add(new JProperty("from", _skip));
            }
            if (!string.IsNullOrWhiteSpace(_highLightStyle))
            {
                parameters.Add(new JProperty("highlight", new JObject(
                    new JProperty("style", _highLightStyle),
                    new JProperty("fields", _highLightFields))));
            }
            if (_fields.Count > 0)
            {
                parameters.Add(new JProperty("fields", _fields));
            }
            if (_facets != null && _facets.Count > 0)
            {
                var facets = new JObject();
                _facets.ForEach(x => facets.Add(x.ToJson()));
                parameters.Add(new JProperty("facets", facets));
            }
            if (_explain.HasValue)
            {
                parameters.Add(new JProperty("explain", _explain));
            }
            if (_sort.Any())
            {
                parameters.Add(new JProperty("sort", _sort));
            }
            return parameters;
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return ToJson().ToString();
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
