using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Retry.Search;
using Couchbase.KeyValue;
using Couchbase.Search.Queries.Vector;

#nullable enable

namespace Couchbase.Search
{
    /// <summary>
    /// A client for making FTS <see cref="ISearchQuery"/> requests and mapping the responses to <see cref="ISearchResult"/>'s.
    /// </summary>
    internal interface ISearchClient
    {
        /// <summary>
        /// Gets the timestamp of the last activity.
        /// </summary>
        DateTime? LastActivity { get; }

        /// <summary>
        /// Executes a <see cref="ISearchQuery"/> request including any <see cref="SearchOptions"/> parameters asynchronously.
        /// </summary>
        /// <returns></returns>
        Task<ISearchResult> QueryAsync(string indexName, FtsSearchRequest ftsSearchRequest, VectorSearch? vectorSearchRequest, IScope? scope, CancellationToken cancellationToken);
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
