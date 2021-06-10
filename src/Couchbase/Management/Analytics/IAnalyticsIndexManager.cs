using Couchbase.Management.Analytics.Link;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Analytics
{
    public interface IAnalyticsIndexManager
    {
        Task CreateDataverseAsync(string dataverseName, CreateAnalyticsDataverseOptions? options = null);

        Task DropDataverseAsync(string dataverseName, DropAnalyticsDataverseOptions? options = null);

        Task CreateDatasetAsync(string datasetName, string bucketName, CreateAnalyticsDatasetOptions? options = null);

        Task DropDatasetAsync(string datasetName, DropAnalyticsDatasetOptions? options = null);

        Task<IEnumerable<AnalyticsDataset>> GetAllDatasetsAsync(GetAllAnalyticsDatasetsOptions? options = null);

        Task CreateIndexAsync(string datasetName, string indexName, Dictionary<string, string> fields, CreateAnalyticsIndexOptions? options = null);

        Task DropIndexAsync(string datasetName, string indexName, DropAnalyticsIndexOptions? options = null);

        Task<IEnumerable<AnalyticsIndex>> GetAllIndexesAsync(GetAllAnalyticsIndexesOptions? options = null);

        Task ConnectLinkAsync(ConnectAnalyticsLinkOptions? options = null);

        Task DisconnectLinkAsync(DisconnectAnalyticsLinkOptions? options = null);

        Task CreateLinkAsync(AnalyticsLink link, CreateAnalyticsLinkOptions? options = null);

        Task ReplaceLinkAsync(AnalyticsLink link, ReplaceAnalyticsLinkOptions? options = null);

        Task DropLinkAsync(string linkName, string dataverseName, DropAnalyticsLinkOptions? options = null);

        Task<IEnumerable<AnalyticsLink>> GetLinks(GetAnalyticsLinksOptions? options = null);

        Task<Dictionary<string, int>> GetPendingMutationsAsync(GetPendingAnalyticsMutationsOptions? options = null);
    }
}
