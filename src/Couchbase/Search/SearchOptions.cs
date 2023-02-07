using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Retry;
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
        internal static SearchOptions Default { get; } = new();
        public static readonly ReadOnly DefaultReadOnly = Default.AsReadOnly();

        private int? _limit;
        private int? _skip;
        private bool? _explain;
        private string?  _highLightStyle;
        private readonly List<string> _fields = new();
        private List<string>? _highLightFields;
        private List<ISearchFacet>? _facets;
        private SearchScanConsistency? _scanConsistency;
        private readonly JArray _sort = new();
        internal CancellationToken Token { get; set; }
        internal TimeSpan? TimeoutValue { get; set; }
        private readonly Dictionary<string, object> _rawParameters = new();
        private Dictionary<string, Dictionary<string, List<object>>> _scanVectors = new();
        private bool _disableScoring;
        private string? _scopeName;
        private string[]? _collectionNames;
        private bool _includeLocations;
        private MutationState? _mutationState;

        internal IRetryStrategy? RetryStrategyValue { get; set; }

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// A parent or external span for tracing.
        /// </summary>
        /// <param name="span">An external <see cref="IRequestSpan"/> implementation for tracing.</param>
        /// <returns></returns>
        public SearchOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Overrides the global <see cref="IRetryStrategy"/> defined in <see cref="ClusterOptions"/> for a request.
        /// </summary>
        /// <param name="retryStrategy">The <see cref="IRetryStrategy"/> to use for a single request.</param>
        /// <returns>The options.</returns>
        public SearchOptions RetryStrategy(IRetryStrategy retryStrategy)
        {
            RetryStrategyValue = retryStrategy;
            return this;
        }

        public SearchOptions DisableScoring(bool disableScoring)
        {
            _disableScoring = disableScoring;
            return this;
        }

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
        /// The name of the scope to target for search results.
        /// </summary>
        /// <param name="scopeName">The name of the scope.</param>
        /// <returns></returns>
        public SearchOptions Scope(string scopeName)
        {
            _scopeName = scopeName;
            return this;
        }

        /// <summary>
        /// The name or names of the collections to target for search results.
        /// </summary>
        /// <param name="collectionNames">The collection names.</param>
        /// <returns></returns>
        public SearchOptions Collections(params string[] collectionNames)
        {
            _collectionNames = collectionNames;
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

        /// <summary>
        /// If set to true, will include the SearchRowLocations.
        /// </summary>
        /// <param name="includeLocations"><see cref="bool"/> indicating that the locations will be returned. The default is false.</param>
        /// <returns><see cref="SearchOptions"/> for chaining method calls.</returns>
        [InterfaceStability(Level.Uncommitted)]
        public SearchOptions IncludeLocations(bool includeLocations)
        {
            _includeLocations = includeLocations;
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

            _mutationState = mutationState;
            return this;
        }

        public JObject ToJson(string? indexName = null)
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

                if (_mutationState != null && _mutationState.Any() && indexName != null)
                {
                    consistency.Add(new JProperty("vectors", JToken.FromObject(_mutationState.ExportForSearch(indexName))));
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
            if (_disableScoring)
            {
                parameters.Add(new JProperty("score", "none"));
            }

            if (_scopeName != null)
            {
                parameters.Add(new JProperty("scope", _scopeName));
            }

            if (_collectionNames != null && _collectionNames.Length > 0)
            {
                parameters.Add(new JProperty("collections", _collectionNames));
            }

            if (_includeLocations)
            {
                parameters.Add(new JProperty("includeLocations", _includeLocations));
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

        public string ToString(string indexName)
        {
            return ToJson(indexName).ToString();
        }

        public void Deconstruct(out int? limit,
            out int? skip,
            out bool? explain,
            out string? highLightStyle,
            out IReadOnlyList<string> fields,
            out IReadOnlyList<string>? highLightFields,
            out IReadOnlyList<ISearchFacet>? facets,
            out SearchScanConsistency? scanConsistency,
            out JArray sort,
            out IReadOnlyDictionary<string, object> rawParameters,
            out IReadOnlyDictionary<string, Dictionary<string, List<object>>> scanVectors,
            out bool disableScoring,
            out string? scopeName,
            out string[]? collectionNames,
            out bool includeLocations,
            out MutationState? mutationState,
            out CancellationToken token,
            out TimeSpan? timeoutValue,
            out IRetryStrategy? retryStrategyValue,
            out IRequestSpan? requestSpanValue)
        {
            limit = _limit;
            skip = _skip;
            explain = _explain;
            highLightStyle = _highLightStyle;
            fields = _fields;
            highLightFields = _highLightFields;
            facets = _facets;
            scanConsistency = _scanConsistency;
            sort = _sort;
            rawParameters = _rawParameters;
            scanVectors = _scanVectors;
            disableScoring = _disableScoring;
            scopeName = _scopeName;
            collectionNames = _collectionNames;
            includeLocations = _includeLocations;
            mutationState = _mutationState;
            token = Token;
            timeoutValue = TimeoutValue;
            retryStrategyValue = RetryStrategyValue;
            requestSpanValue = RequestSpanValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(
                out int? limit,
                out int? skip,
                out bool? explain,
                out string? highLightStyle,
                out IReadOnlyList<string> fields,
                out IReadOnlyList<string>? highLightFields,
                out IReadOnlyList<ISearchFacet>? facets,
                out SearchScanConsistency? scanConsistency,
                out JArray sort,
                out IReadOnlyDictionary<string, object> rawParameters,
                out IReadOnlyDictionary<string, Dictionary<string, List<object>>> scanVectors,
                out bool disableScoring,
                out string? scopeName,
                out string[]? collectionNames,
                out bool includeLocations,
                out MutationState? mutationState,
                out CancellationToken token,
                out TimeSpan? timeoutValue,
                out IRetryStrategy? retryStrategyValue,
                out IRequestSpan? requestSpanValue);

            return new ReadOnly(
                limit,
                skip,
                explain,
                highLightStyle,
                fields,
                highLightFields,
                facets,
                scanConsistency,
                sort,
                rawParameters,
                scanVectors,
                disableScoring,
                scopeName,
                collectionNames,
                includeLocations,
                mutationState,
                token,
                timeoutValue,
                retryStrategyValue,
                requestSpanValue);
        }

        public record ReadOnly(
            int? Limit,
            int? Skip,
            bool? Explain,
            string? HighLightStyle,
            IReadOnlyList<string> Fields,
            IReadOnlyList<string>? HighLightFields,
            IReadOnlyList<ISearchFacet>? Facets,
            SearchScanConsistency? ScanConsistency,
            JArray Sort,
            IReadOnlyDictionary<string, object> RawParameters,
            IReadOnlyDictionary<string, Dictionary<string, List<object>>> ScanVectors,
            bool DisableScoring,
            string? ScopeName,
            string[]? CollectionNames,
            bool IncludeLocations,
            MutationState? MutationState,
            CancellationToken Token,
            TimeSpan? TimeoutValue,
            IRetryStrategy? RetryStrategy,
            IRequestSpan? RequestSpan);
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
