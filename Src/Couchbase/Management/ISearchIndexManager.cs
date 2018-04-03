using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface ISearchIndexManager
    {
        /// <summary>
        /// Gets all search index definitions asynchronously.
        /// </summary>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        Task<IResult<string>> GetAllSearchIndexDefinitionsAsync(CancellationToken token = default (CancellationToken));

        /// <summary>
        /// Gets the search index definition asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        Task<IResult<string>> GetSearchIndexDefinitionAsync(string indexName, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Creates a search index asynchronously.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        Task<IResult<string>> CreateSearchIndexAsync(SearchIndexDefinition definition, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Deletes the search index asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        Task<IResult> DeleteSearchIndexAsync(string indexName, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Gets the search index document count asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        Task<IResult<int>> GetSearchIndexDocumentCountAsync(string indexName, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Sets the search index ingestion mode asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="ingestionMode">The ingestion mode.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult> SetSearchIndexIngestionModeAsync(string indexName, SearchIndexIngestionMode ingestionMode, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Sets the search index query mode asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="queryMode">The query mode.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult> SetSearchIndexQueryModeAsync(string indexName, SearchIndexQueryMode queryMode, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Sets the search index plan mode asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="planFreezeMode">The plan freeze mode.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult> SetSearchIndexPlanModeAsync(string indexName, SearchIndexPlanFreezeMode planFreezeMode, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Gets the search index statistics asynchronously.
        /// </summary>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult<string>> GetSearchIndexStatisticsAsync(CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Gets the search index statistics asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult<string>> GetSearchIndexStatisticsAsync(string indexName, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Gets all search index partition information asynchronously.
        /// </summary>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult<string>> GetAllSearchIndexPartitionInfoAsync(CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Gets the search index partition information asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult<string>> GetSearchIndexPartitionInfoAsync(string indexName, CancellationToken token = default(CancellationToken));

        /// <summary>
        /// Gets the search index partition document count asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="token">A <see cref="CancellationToken"/> token.</param>
        /// <remarks>Uncommitted / Experimental</remarks>
        Task<IResult<int>> GetSearchIndexPartitionDocumentCountAsync(string indexName, CancellationToken token = default(CancellationToken));
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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
