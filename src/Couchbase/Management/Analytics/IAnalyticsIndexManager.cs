using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Analytics
{
    public interface IAnalyticsIndexManager
    {
        Task CreateDataverseAsync(string dataverseName, CreateAnalyticsDataverseOptions options = null);

        Task DropDataverseAsync(string dataverseName, DropAnalyticsDataverseOptions options = null);

        Task CreateDatasetAsync(string bucketName, string datasetName, CreateAnalyticsDatasetOptions options = null);

        Task DropDatasetAsync(string datasetName, DropAnalyticsDatasetOptions options = null);

        Task<IEnumerable<AnalyticsDataset>> GetAllDatasetsAsync(GetAllAnalyticsDatasetsOptions options = null);

        Task CreateIndexAsync(string indexName, Dictionary<string, string> fields, CreateAnalyticsIndexOptions options = null);

        Task DropIndexAsync(string datasetName, string indexName, DropAnalyticsIndexOptions options = null);

        Task<IEnumerable<AnalyticsIndex>> GetAllIndexesAsync(GetAllAnalyticsIndexesOptions options = null);

        Task ConnectLinkAsync(string linkName, ConnectAnalyticsLinkOptions options = null);

        Task DisconnectLinkAsync(string linkName, DisconnectAnalyticsLinkOptions options = null);

        Task<Dictionary<string, int>> GetPendingMutationsAsync(GetPendingAnalyticsMutationsOptions options = null);
    }
}
