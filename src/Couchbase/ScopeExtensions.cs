using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.KeyValue;
using Couchbase.Query;

namespace Couchbase
{
    public static class ScopeExtensions
    {
        #region Query

        /// <summary>
        /// Executes a N1QL query on the server.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="scope">The calling scope.</param>
        /// <param name="statement">The statement to execute.</param>
        /// <param name="configureOptions">Any options as a lambda.</param>
        /// <returns>A Task that can be awaited for the results of the query.</returns>
        public static Task<IQueryResult<T>> QueryAsync<T>(this IScope scope, string statement,
            Action<QueryOptions> configureOptions)
        {
            var options = new QueryOptions();
            configureOptions?.Invoke(options);

            return scope.QueryAsync<T>(statement, options);
        }

        /// <summary>
        /// Executes a N1QL query on the server.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="scope">The calling scope.</param>
        /// <param name="statement">The statement to execute.</param>
        /// <returns>A Task that can be awaited for the results of the query.</returns>
        public static Task<IQueryResult<T>> QueryAsync<T>(this IScope scope, string statement)
        {
            return scope.QueryAsync<T>(statement, QueryOptions.Create(statement));
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Executes a analytics query on the server.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="scope">The calling scope.</param>
        /// <param name="statement">The statement to execute.</param>
        /// <param name="configureOptions"></param>
        /// <returns>A Task that can be awaited for the results of the query.</returns>
        public static Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(this IScope scope, string statement,
            Action<AnalyticsOptions> configureOptions)
        {
            var options = new AnalyticsOptions();
            configureOptions(options);

            return scope.AnalyticsQueryAsync<T>(statement, options);
        }

        #endregion
    }
}
