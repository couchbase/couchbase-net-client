using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management.Analytics
{
    public interface IAnalyticsIndexManager
    {
        Task CreateDataverseAsync(string dataverseName, CreateAnalyticsDataverseOptions options);

        Task DropDataverseAsync(string dataverseName, DropAnalyticsDataverseOptions options);

        Task CreateDatasetAsync(string bucketName, string datasetName, CreateAnalyticsDatasetOptions options);

        Task DropDatasetAsync(string datasetName, DropAnalyticsDatasetOptions options);

        Task<IEnumerable<AnalyticsDataset>> GetAllDatasetsAsync(GetAllAnalyticsDatasetsOptions options);

        Task CreateIndexAsync(string indexName, Dictionary<string, string> fields, CreateAnalyticsIndexOptions options);

        Task DropIndexAsync(string datasetName, string indexName, DropAnalyticsIndexOptions options);

        Task<IEnumerable<AnalyticsIndex>> GetAllIndexesAsync(GetAllAnalyticsIndexesOptions options);

        Task ConnectLinkAsync(string linkName, ConnectAnalyticsLinkOptions options);

        Task DisconnectLinkAsync(string linkName, DisconnectAnalyticsLinkOptions options);

        Task<Dictionary<string, int>> GetPendingMutationsAsync(GetPendingAnalyticsMutationsOptions options);
    }
}
