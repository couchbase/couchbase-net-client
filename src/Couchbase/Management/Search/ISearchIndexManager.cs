using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Search
{
    public interface ISearchIndexManager
    {
        Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions options = null);

        Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions options = null);

        Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions options = null);

        Task DropIndexAsync(string indexName, DropSearchIndexOptions options = null);

        Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions options = null);

        Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions options = null);

        Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions options = null);

        Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions options = null);

        Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions options = null);

        Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions options = null);

        Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions options = null);

        //Task<IEnumerable<JSONObject>> AnalyzeDocumentAsync(string indexName, JSONObject document, AnalyzeDocOptions options);
    }
}
