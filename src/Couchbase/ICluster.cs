using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Diagnostics;
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
        ValueTask<IBucket> BucketAsync(string name);

        #region Diagnostics

        /// <summary>
        /// Actively performs I/O by application-level pinging services and returning their pinged status.
        /// </summary>
        /// <param name="options">Optional arguments.</param>
        /// <returns></returns>
        Task<IPingReport> PingAsync(PingOptions? options = null);

        /// <summary>
        /// Waits until a desired cluster state by default (“online”) is reached or times out.
        /// </summary>
        /// <param name="timeout">The <see cref="TimeSpan"/> duration to wait before throwing an exception.</param>
        /// <param name="options">Optional arguments.</param>
        /// <returns></returns>
        Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null);

        /// <summary>
        /// Creates diagnostic report that can be used to determine the healthfulness of the cluster. It does not proactively perform any I/O against the network.
        /// </summary>
        /// <param name="options">Optional arguments.</param>
        /// <returns></returns>
        Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null);

        #endregion

        #region Query

        Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options);

        Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default);

        Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = default);

        #endregion

        #region Management
        IQueryIndexManager QueryIndexes { get; }

        IAnalyticsIndexManager AnalyticsIndexes { get; }

        ISearchIndexManager SearchIndexes { get; }

        IBucketManager Buckets { get; }

        IUserManager Users { get; }
        #endregion
    }
}
