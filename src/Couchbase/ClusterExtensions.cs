using System;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics;
using Couchbase.Services.Analytics;
using Couchbase.Services.Query;
using Couchbase.Services.Search;

namespace Couchbase
{
    public static class ClusterExtensions
    {
        public static Task<IDiagnosticsReport> Diagnostics(this ICluster cluster)
        {
            throw new NotImplementedException();
        }

        public static Task<IQueryResult<T>> QueryAsync<T>(this ICluster cluster, string statement,
            Action<QueryParameter> parameters = null, Action<QueryOptions> options = null)
        {
            var queryParameters = new QueryParameter();
            parameters?.Invoke(queryParameters);

            var queryOptions = new QueryOptions();
            options?.Invoke(queryOptions);

            return cluster.QueryAsync<T>(statement, queryParameters, queryOptions);
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
