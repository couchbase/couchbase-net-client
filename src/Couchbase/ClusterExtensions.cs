using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Diagnostics;
using Couchbase.Query;
using Couchbase.Search;

namespace Couchbase
{
    public static class ClusterExtensions
    {
        public static Task<IDiagnosticsReport> DiagnosticsAsync(this ICluster cluster, Action<DiagnosticsOptions> configureOptions)
        {
            var options = new DiagnosticsOptions();
            configureOptions?.Invoke(options);

            return cluster.DiagnosticsAsync(options);
        }

        public static Task<IQueryResult<T>> QueryAsync<T>(this ICluster cluster, string statement,
            Action<QueryOptions> configureOptions)
        {
            var options = new QueryOptions();
            configureOptions?.Invoke(options);

            return cluster.QueryAsync<T>(statement, options);
        }

        public static Task<IQueryResult<T>> QueryAsync<T>(this ICluster cluster, string statement)
        {
            return cluster.QueryAsync<T>(statement, QueryOptions.Create(statement));
        }

        #region Analytics

        public static IAnalyticsResult<T> AnalyticsQuery<T>(this ICluster cluster, string statement,
            Action<AnalyticsOptions> configureOptions)
        {
            var options = new AnalyticsOptions();
            configureOptions(options);

            return cluster.AnalyticsQueryAsync<T>(statement, options)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public static IAnalyticsResult<T> AnalyticsQuery<T>(this ICluster cluster, string statement,
            AnalyticsOptions options = default)
        {
            return cluster.AnalyticsQueryAsync<T>(statement, options)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public static Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(this ICluster cluster, string statement,
            Action<AnalyticsOptions> configureOptions)
        {
            var options = new AnalyticsOptions();
            configureOptions(options);

            return cluster.AnalyticsQueryAsync<T>(statement, options);
        }

        #endregion

        #region Search

        public static Task<ISearchResult> SearchQueryAsync(this ICluster cluster, string indexName, ISearchQuery query,
            Action<ISearchOptions> configureOptions)
        {
            var options = new SearchOptions();
            configureOptions(options);

            return cluster.SearchQueryAsync(indexName, query, options);
        }

        #endregion
    }
}
