using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Search
{
    public interface ISearchIndexManager
    {
        Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions options);

        Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions options);

        Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions options);

        Task DropIndexAsync(string indexName, DropSearchIndexOptions options);

        Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions options);

        Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions options);

        Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions options);

        Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions options);

        Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions options);

        Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions options);

        Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions options);

        //Task<IEnumerable<JSONObject>> AnalyzeDocumentAsync(string indexName, JSONObject document, AnalyzeDocOptions options);
    }
}
