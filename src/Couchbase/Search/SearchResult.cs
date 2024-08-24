using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Couchbase.Core.Retry;
using Newtonsoft.Json;

namespace Couchbase.Search
{
    /// <summary>
    /// The result of a search query.
    /// </summary>
    /// <seealso cref="ISearchResult" />
    internal sealed class SearchResult : ISearchResult, IServiceResultExceptionInfo, IDisposable
    {
        internal SearchResult()
        {
            Hits = new List<ISearchQueryRow>();
            Facets = new Dictionary<string, IFacetResult>();
            MetaData = new MetaData();
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
        /// The rows returned by the search request.
        /// </summary>
        [JsonProperty("hits")]
        public IList<ISearchQueryRow> Hits { get; internal set; }

        /// <summary>
        /// The facets for the result.
        /// </summary>
        [JsonProperty("facets")]
        public IDictionary<string, IFacetResult> Facets { get; internal set; }

        /// <summary>
        /// The search result metadata.
        /// </summary>
        [JsonProperty("metaData")]
        public MetaData MetaData { get; }

        /// <summary>
        /// Adds the specified row.
        /// </summary>
        /// <param name="row">The row.</param>
        internal void Add(ISearchQueryRow row)
        {
            Hits.Add(row);
        }

        internal bool ShouldRetry()
        {
            SetRetryReasonIfFailed();
            return RetryReason != RetryReason.NoRetry;
        }

        internal void SetRetryReasonIfFailed()
        {
            if ((int) HttpStatusCode == 429) // 429 - TooManyRequests
            {
                RetryReason = RetryReason.SearchTooManyRequests;
            }
        }

        public RetryReason RetryReason { get; private set; } = RetryReason.NoRetry;

        public Exception NoRetryException { get; set; }

        internal HttpStatusCode HttpStatusCode { get; set; }

        public void Dispose()
        { }
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
