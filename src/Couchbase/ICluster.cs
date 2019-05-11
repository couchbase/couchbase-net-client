using System;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics;
using Couchbase.Management;
using Couchbase.Services.Analytics;
using Couchbase.Services.Query;
using Couchbase.Services.Search;

namespace Couchbase
{
    public interface ICluster : IDisposable
    {
        Task<IBucket> Bucket(string name);

        Task<IDiagnosticsReport> Diagnostics(string reportId);

        Task<IClusterManager> ClusterManager();

        #region Query

        Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryParameter parameters = null, IQueryOptions options = null);

        Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions options = default);

        Task<ISearchResult> SearchQueryAsync(string indexName, SearchQuery query, ISearchOptions options = default);

        #endregion

        IQueryIndexes QueryIndexes { get; }

        IAnalyticsIndexes AnalyticsIndexes { get; }

        ISearchIndexes SearchIndexes { get; }

        IBucketManager Buckets { get; }

        IUserManager Users { get; }
    }
}
