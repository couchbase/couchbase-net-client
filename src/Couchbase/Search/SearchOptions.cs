using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Couchbase.Query;
using Couchbase.Search.Sort;
using Couchbase.Utils;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Search
{
    /// <summary>
    /// Represents a number of query options that can be applied to a FTS query request.
    /// </summary>
    public class SearchOptions
    {
        private int? _limit;
        private int? _skip;
        private bool? _explain;
        private string?  _highLightStyle;
        private readonly List<string> _fields = new List<string>();
        private List<string>? _highLightFields;
        private List<ISearchFacet>? _facets;
        private SearchScanConsistency? _scanConsistency;
        private readonly JArray _sort = new JArray();
        internal  CancellationToken Token { get; set; }
        internal TimeSpan? TimeoutValue { get; set; }
        private readonly Dictionary<string, object> _rawParameters = new Dictionary<string, object>();
        private Dictionary<string, Dictionary<string, List<object>>> _scanVectors = new Dictionary<string, Dictionary<string, List<object>>>();

        public SearchOptions CancellationToken(CancellationToken token)
        {
            Token = token;
            return this;
        }

        /// <summary>
        /// Limits the number of matching results from a returned result-set.
        /// </summary>
        /// <param name="limit"></param>
        /// <returns></returns>
        public SearchOptions Limit(int limit)
        {
            _limit = limit;
            return this;
        }

        /// <summary>
        /// Skip indicates how many matching results to skip on the result set before returning matches.
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>
        public SearchOptions Skip(int skip)
        {
            _skip = skip;
            return this;
        }

        /// <summary>
        /// If true, the response will include additional search score explanations.
        /// </summary>
        /// <param name="explain"></param>
        /// <returns></returns>
        public SearchOptions Explain(bool explain)
        {
            _explain = explain;
            return this;
        }

        /// <summary>
        /// Allows setting of additional highlighting on the result set of matching terms.
        /// </summary>
        /// <param name="highLightStyle">The <see cref="HighLightStyle" /> to use.</param>
        /// <returns></returns>
        public SearchOptions Highlight(HighLightStyle highLightStyle)
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
        public SearchOptions Highlight(HighLightStyle highLightStyle, params string[] fields)
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
        public SearchOptions Fields(params string[] fields)
        {
            if(fields == null || fields.Length <= 0)
            {
                throw new ArgumentNullException(nameof(fields), "must be non-null and have at least one value.");
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
        ///   <see cref="ISearchFacet" />s used to aggregate information collected on a particular result set.
        /// </summary>
        /// <param name="searchFacets">The <see cref="ISearchFacet" /> to aggregate information on.</param>
        /// <returns></returns>
        public SearchOptions Facets(params ISearchFacet[] searchFacets)
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
        public SearchOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        /// <summary>
        /// The <see cref="SearchScanConsistency" /> you require for you <see cref="ISearchResult" />s.
        /// </summary>
        /// <param name="consistency">The <see cref="SearchScanConsistency" /> for documents to be included in the query results.</param>
        /// <returns></returns>
        public SearchOptions ScanConsistency(SearchScanConsistency consistency)
        {
            _scanConsistency = consistency;
            return this;
        }

        /// <summary>
        /// Configures the list of fields which are used for sorting the search result. Fields with a prefix of "-" indicate a descending nature.
        /// If no sort is provided, it is equal to sort("-_score"), since the server will sort it by score in descending order by default.
        /// </summary>
        /// <param name="sort">The field names to sort by.</param>
        /// <returns></returns>
        public SearchOptions Sort(params string[] sort)
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
        public SearchOptions Sort(ISearchSort sort)
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
        public SearchOptions Sort(JObject sort)
        {
            if (sort != null)
            {
                _sort.Add(sort);
            }
            return this;
        }

        public SearchOptions Raw(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Parameter name cannot be null or empty.");
            }

            _rawParameters.Add(name, value);
            return this;
        }

        public SearchOptions ConsistentWith(MutationState mutationState)
        {
#pragma warning disable 618
            ScanConsistency(SearchScanConsistency.AtPlus);
#pragma warning restore 618
            _scanVectors = new Dictionary<string, Dictionary<string, List<object>>>();
            foreach (var token in mutationState)
            {
                if (_scanVectors.TryGetValue(token.BucketRef, out var vector))
                {
                    var bucketId = token.VBucketId.ToString();
                    if (vector.TryGetValue(bucketId, out var bucketRef))
                    {
                        if ((long)bucketRef.First() < token.SequenceNumber)
                        {
                            vector[bucketId] = new List<object>
                            {
                                token.SequenceNumber,
                                token.VBucketUuid.ToString()
                            };
                        }
                    }
                    else
                    {
                        vector.Add(token.VBucketId.ToString(),
                            new List<object>
                            {
                                token.SequenceNumber,
                                token.VBucketUuid.ToString()
                            });
                    }
                }
                else
                {
                    _scanVectors.Add(token.BucketRef, new Dictionary<string, List<object>>
                    {
                        {
                            token.VBucketId.ToString(),
                            new List<object>
                            {
                                token.SequenceNumber,
                                token.VBucketUuid.ToString()
                            }
                        }
                    });
                }
            }

            return this;
        }

        public JObject ToJson()
        {
            var ctl = new JObject();
            if (TimeoutValue.HasValue)
            {
                ctl.Add(new JProperty("timeout", (long)TimeoutValue.Value.TotalMilliseconds));
            }
            if (_scanConsistency.HasValue)
            {
                var consistency = new JObject(
                        new JProperty("level", _scanConsistency.GetDescription()));

                if (_scanVectors != null && _scanVectors.Count > 0)
                {
                    consistency.Add(new JProperty("vectors", _scanVectors));
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
            foreach (var rawParameter in _rawParameters)
            {
                parameters.Add(new JProperty(rawParameter.Key, rawParameter.Value));
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
