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
        Task Initialize();

        Task<IBucket> this[string name] { get; }

        Task<IBucket> Bucket(string name);

        Task<IDiagnosticsReport> Diagnostics();

        Task<IDiagnosticsReport> Diagnostics(string reportId);

        Task<IClusterManager> ClusterManager();

       // Task<IQueryResult<T>> Query<T>(string statement);

        Task<IQueryResult<T>> Query<T>(string statement, QueryParameter parameters = null, IQueryOptions options = null);

        Task<IQueryResult<T>> Query<T>(string statement, Action<QueryParameter> parameters = null,  Action<IQueryOptions> options = null);

        Task<IAnalyticsResult> AnalyticsQuery<T>(string statement, IAnalyticsOptions options);

        Task<ISearchResult> SearchQuery<T>(ISearchQuery query, ISearchOptions options);

        IQueryIndexes QueryIndexes { get; }

        IAnalyticsIndexes AnalyticsIndexes { get; }

        ISearchIndexes SearchIndexes { get; }

        IBucketManager Buckets { get; }

        IUserManager Users { get; }
    }
}
