using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core.Compatibility;
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

#if NET6_0_OR_GREATER
        // Note: We chose a different name here, QueryInterpolatedAsync, to avoid confusion with
        // QueryAsync(string statement, QueryOptions options = default). If we use the same name then the compiler
        // chooses the method directly on ICluster, missing our interpolation logic.
        //
        // There is also a security risk for a consumer who may unintentionally apply an interpolated string to a
        // method thinking it is being securely parameterized when it is not. Using a different method named helps
        // to make this clear.
        //
        // Finally, using a different name ensures that consumers don't receive a behavioral change upon upgrade.
        // Consumers may be using string interpolation to build statements already and expecting them to be raw strings
        // (i.e. including wrapping quotes, etc). Upon upgrading to .NET 6 this behavior would suddenly change.
        // By using a different name, the consumer opts into the behavior.

        /// <summary>
        /// Executes an interpolated query.
        /// </summary>
        /// <typeparam name="T">Type of row returned by the query.</typeparam>
        /// <param name="cluster">Cluster against which to execute the query.</param>
        /// <param name="handler">The interpolated string.</param>
        /// <returns>The query result.</returns>
        /// <remarks>
        /// <para>
        /// Treats expressions in the interpolated string as positional parameters. This makes prepared queries with dynamic
        /// parameters very easy to build. Note that format strings and alignment are ignored. Also, you may only use expressions
        /// in locations in the query where parameters are acceptable.
        /// </para>
        /// <para>
        /// This overload executes the query with AdHoc <c>false</c>, resulting in prepared queries.
        /// </para>
        /// </remarks>
        [InterfaceStability(Level.Volatile)]
        public static Task<IQueryResult<T>> QueryInterpolatedAsync<T>(this ICluster cluster, ref QueryInterpolatedStringHandler handler)
        {
            // handler.QueryOptions will already be filled with positional parameters and AdHoc(false) when we reach this point

            return cluster.QueryAsync<T>(handler.ToStringAndClear(), handler.QueryOptions);
        }

        /// <summary>
        /// Executes an interpolated query.
        /// </summary>
        /// <typeparam name="T">Type of row returned by the query.</typeparam>
        /// <param name="cluster">Cluster against which to execute the query.</param>
        /// <param name="configureOptions">Action to configure the <see cref="QueryOptions"/>.</param>
        /// <param name="handler">The interpolated string.</param>
        /// <returns>The query result.</returns>
        /// <remarks>
        /// <para>
        /// Treats expressions in the interpolated string as positional parameters. This makes prepared queries with dynamic
        /// parameters very easy to build. Note that format strings and alignment are ignored. Also, you may only use expressions
        /// in locations in the query where parameters are acceptable.
        /// </para>
        /// <para>
        /// This overload defaults AdHoc to <c>false</c>, resulting in prepared queries. This may be overridden in
        /// <paramref name="configureOptions"/>.
        /// </para>
        /// </remarks>
        [InterfaceStability(Level.Volatile)]
        public static Task<IQueryResult<T>> QueryInterpolatedAsync<T>(this ICluster cluster, Action<QueryOptions> configureOptions,
            ref QueryInterpolatedStringHandler handler)
        {
            // handler.QueryOptions will already be filled with positional parameters and AdHoc(false) when we reach this point

            var options = handler.QueryOptions;
            configureOptions.Invoke(options);

            return cluster.QueryAsync<T>(handler.ToStringAndClear(), options);
        }

        /// <summary>
        /// Executes an interpolated query.
        /// </summary>
        /// <typeparam name="T">Type of row returned by the query.</typeparam>
        /// <param name="cluster">Cluster against which to execute the query.</param>
        /// <param name="options">Options to control query execution. Should not include any positional parameters.</param>
        /// <param name="handler">The interpolated string.</param>
        /// <returns>The query result.</returns>
        /// <remarks>
        /// <para>
        /// Treats expressions in the interpolated string as positional parameters. This makes prepared queries with dynamic
        /// parameters very easy to build. Note that format strings and alignment are ignored. Also, you may only use expressions
        /// in locations in the query where parameters are acceptable.
        /// </para>
        /// <para>
        /// This overload does not default AdHoc to <c>false</c> like the other overloads. If you desire prepared queries,
        /// be sure to set AdHoc to <c>false</c> in <paramref name="options"/>.
        /// </para>
        /// </remarks>
        [InterfaceStability(Level.Volatile)]
        public static Task<IQueryResult<T>> QueryInterpolatedAsync<T>(this ICluster cluster, QueryOptions options,
            [InterpolatedStringHandlerArgument("options")] ref QueryInterpolatedStringHandler handler)
        {
            // options will be passed to the constructor of QueryInterpolatedStringHandler so it adds positional
            // parameters as the query string is being built

            return cluster.QueryAsync<T>(handler.ToStringAndClear(), handler.QueryOptions);
        }

#endif

        #region Analytics

        [Obsolete("Use the async version instead.")]
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

        [Obsolete("Use the async version instead.")]
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
            Action<SearchOptions> configureOptions)
        {
            var options = new SearchOptions();
            configureOptions(options);

            return cluster.SearchQueryAsync(indexName, query, options);
        }

        #endregion
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
