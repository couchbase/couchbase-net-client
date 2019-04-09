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

        Task<IDiagnosticsReport> Diagnostics();

        Task<IDiagnosticsReport> Diagnostics(string reportId);

        Task<IClusterManager> ClusterManager();

        Task<IQueryResult<T>> Query<T>(string statement, QueryParameter parameters = null, IQueryOptions options = null);

        Task<IQueryResult<T>> Query<T>(string statement, Action<QueryParameter> parameters = null, Action<IQueryOptions> options = null);

        Task<IAnalyticsResult> AnalyticsQuery<T>(string statement, IAnalyticsOptions options);

        #region Search

        ISearchResult SearchQuery(string indexName, SearchQuery query, Action<ISearchOptions> options);
        ISearchResult SearchQuery(string indexName, SearchQuery query, ISearchOptions options = default);

        Task<ISearchResult> SearchQueryAsync(string indexName, SearchQuery query, Action<ISearchOptions> options);
        Task<ISearchResult> SearchQueryAsync(string indexName, SearchQuery query, ISearchOptions options = default);

        #endregion

        IQueryIndexes QueryIndexes { get; }

        IAnalyticsIndexes AnalyticsIndexes { get; }

        ISearchIndexes SearchIndexes { get; }

        IBucketManager Buckets { get; }

        IUserManager Users { get; }
    }
}
