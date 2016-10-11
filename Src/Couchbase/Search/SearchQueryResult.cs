using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Couchbase.Configuration.Client;
using Couchbase.Core;

namespace Couchbase.Search
{
    /// <summary>
    /// The result of a search query.
    /// </summary>
    /// <seealso cref="Couchbase.Search.ISearchQueryResult" />
    public class SearchQueryResult : ISearchQueryResult, IDisposable
    {
        private readonly HttpClient _httpClient;

        internal SearchQueryResult()
        {
            Hits = new List<ISearchQueryRow>();
            Facets = new List<SearchFacet>();
            Errors = new List<string>();
        }

        internal SearchQueryResult(HttpClient httpClient)
            : this()
        {
            _httpClient = httpClient;
        }

        public IEnumerator<ISearchQueryRow> GetEnumerator()
        {
            return Hits.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// The total count.
        /// </summary>
        public long SuccessCount { get; internal set; }

        /// <summary>
        /// The count of errors.
        /// </summary>
        public long ErrorCount { get; internal set; }

        /// <summary>
        /// Gets the total count.
        /// </summary>
        /// <value>
        /// The total count.
        /// </value>
        public long TotalCount { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="ISearchQueryResult" /> is success.
        /// </summary>
        /// <value>
        ///   <c>true</c> if success; otherwise, <c>false</c>.
        /// </value>
        public bool Success { get; internal set; }

        /// <summary>
        /// If the operation wasn't succesful, the first message returned in the <see cref="Errors"/>
        /// </summary>
        [Obsolete("Use the Errors collection.")]
        string IResult.Message { get { return string.Empty; } }

        /// <summary>
        /// Total time taken for the results.
        /// </summary>
        public TimeSpan Took { get; internal set; }

        /// <summary>
        /// Total hits returned by the results.
        /// </summary>
        public long TotalHits { get; internal set; }

        /// <summary>
        /// The maximum score within the results.
        /// </summary>
        public double MaxScore { get; internal set; }

        /// <summary>
        /// The rows returned by the query request.
        /// </summary>
        public IList<ISearchQueryRow> Hits { get; internal set; }

        /// <summary>
        /// The facets for the result.
        /// </summary>
        /// <value>
        /// The facets.
        /// </value>
        public IList<SearchFacet> Facets { get; internal set; }

        /// <summary>
        /// The errors returned from the server if the request failed.
        /// </summary>
        /// <value>
        /// The errors.
        /// </value>
        public IList<string> Errors { get; internal set; }

        /// <summary>
        /// Sets the lifespan of the search request; used to check if the request exceeded the maximum time
        /// configured for it in <see cref="ClientConfiguration.SearchRequestTimeout" />
        /// </summary>
        /// <value>
        /// The lifespan.
        /// </value>
        public Lifespan Lifespan { get; set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Adds the specified row.
        /// </summary>
        /// <param name="row">The row.</param>
        internal void Add(ISearchQueryRow row)
        {
            Hits.Add(row);
        }

        /// <summary>
        /// Adds the specified facet.
        /// </summary>
        /// <param name="facet">The facet.</param>
        internal void Add(SearchFacet facet)
        {
            Facets.Add(facet);
        }

        bool IResult.ShouldRetry()
        {
            throw new NotImplementedException();
        }

        public SearchStatus Status { get; set; }

        public void Dispose()
        {
            if (_httpClient != null)
            {
                _httpClient.Dispose();
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Status: {0}", Status);
            if (Errors != null)
            {
                foreach (var error in Errors)
                {
                    sb.AppendFormat("Error: {0}", error);
                }
            }
            if (Exception != null)
            {
                sb.AppendFormat("Exception: {0}", Exception);
            }
            return sb.ToString();
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
