using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Search
{
    public interface ISearchIndexManager
    {
        Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions? options = null);

        Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions? options = null);

        Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions? options = null);

        Task DropIndexAsync(string indexName, DropSearchIndexOptions? options = null);

        Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions? options = null);

        Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions? options = null);

        Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions? options = null);

        Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions? options = null);

        Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions? options = null);

        Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions? options = null);

        Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions? options = null);

        //Task<IEnumerable<JSONObject>> AnalyzeDocumentAsync(string indexName, JSONObject document, AnalyzeDocOptions options);
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
