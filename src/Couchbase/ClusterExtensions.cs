using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Diagnostics;
using Couchbase.Query;
using Couchbase.Search;

namespace Couchbase
{
    public static class ClusterExtensions
    {
        public static Task<IDiagnosticsReport> Diagnostics(this ICluster cluster)
        {
            throw new NotImplementedException();
        }

        public static Task<Query.IQueryResult<T>> QueryAsync<T>(this ICluster cluster, string statement,
            Action<QueryOptions> configureOptions)
        {
            var options = new QueryOptions();
            configureOptions?.Invoke(options);

            return cluster.QueryAsync<T>(statement, options);
        }

        public static Task<Query.IQueryResult<T>> QueryAsync<T>(this ICluster cluster, string statement)
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

        public static ISearchResult SearchQuery(this ICluster cluster, string indexName, SearchQuery query,
            Action<ISearchOptions> configureOptions)
        {
            var options = new SearchOptions();
            configureOptions(options);

            return cluster.SearchQueryAsync(indexName, query, options)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public static ISearchResult SearchQuery(this ICluster cluster, string indexName, SearchQuery query,
            ISearchOptions options = default)
        {
            return cluster.SearchQueryAsync(indexName, query, options)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public static Task<ISearchResult> SearchQueryAsync(this ICluster cluster, string indexName, SearchQuery query,
            Action<ISearchOptions> configureOptions)
        {
            var options = new SearchOptions();
            configureOptions(options);

            return cluster.SearchQueryAsync(indexName, query, options);
        }

        #endregion
    }
}
