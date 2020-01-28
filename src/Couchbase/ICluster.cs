using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Diagnostics;
using Couchbase.Management;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Query;
using Couchbase.Search;

#nullable enable

namespace Couchbase
{
    public interface ICluster : IDisposable
    {
        Task<IBucket> BucketAsync(string name);

        Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null);

        #region Query

        Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options);

        Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default);

        Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, ISearchOptions? options = default);

        #endregion

        IQueryIndexManager QueryIndexes { get; }

        IAnalyticsIndexManager AnalyticsIndexes { get; }

        ISearchIndexManager SearchIndexes { get; }

        IBucketManager Buckets { get; }

        IUserManager Users { get; }

    }
}
