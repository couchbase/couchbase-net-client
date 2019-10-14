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

namespace Couchbase
{
    public interface ICluster : IDisposable
    {
        Task<IBucket> BucketAsync(string name);

        Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions options = null);

        Task<IClusterManager> ClusterManagerAsync();

        #region Query

        Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options);

        Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions options = default);

        Task<ISearchResult> SearchQueryAsync(string indexName, SearchQuery query, ISearchOptions options = default);

        #endregion

        IQueryIndexManager QueryIndexes { get; }

        IAnalyticsIndexManager AnalyticsIndexes { get; }

        ISearchIndexManager SearchIndexes { get; }

        IBucketManager Buckets { get; }

        IUserManager Users { get; }

        /// <summary>
        /// Exports a deferred analytics query handle into an encoded format.
        /// <para>NOTE: This is an experimental feature is subject to change.</para>
        /// </summary>
        /// <typeparam name="T">The type to deserialize the results to.</typeparam>
        /// <param name="handle">The deferred analytics query handle.</param>
        /// <returns>The encoded query handle as a JSON <see cref="string"/>.</returns>
        string ExportDeferredAnalyticsQueryHandle<T>(IAnalyticsDeferredResultHandle<T> handle);

        /// <summary>
        /// Imports a deferred analytics query handle.
        /// <para>NOTE: This is an experimental feature is subject to change.</para>
        /// </summary>
        /// <typeparam name="T">The type to deserialze results to</typeparam>
        /// <param name="encodedHandle">The encoded query handle.</param>
        /// <returns>An instance of <see cref="IAnalyticsDeferredResultHandle{T}"/> that can be sued to retrieve results
        /// from an deferred analytics query.</returns>
        IAnalyticsDeferredResultHandle<T> ImportDeferredAnalyticsQueryHandle<T>(string encodedHandle);
    }
}
