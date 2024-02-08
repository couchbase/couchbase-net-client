#if NET5_0_OR_GREATER
#nullable enable
using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Query;

namespace Couchbase.Integrated.Transactions.Config
{
    /// <summary>
    /// Extension methods with more convenient overloads for using options and configuration.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Run a single query as a transaction.
        /// </summary>
        /// <typeparam name="T">The type of the result.  Use <see cref="object"/> for queries with no results.</typeparam>
        /// <param name="transactions">The transactions object to query from.</param>
        /// <param name="statement">The statement to execute.</param>
        /// <param name="configure">An action to configure this transaction.</param>
        /// <param name="scope">The scope</param>
        /// <returns>A <see cref="SingleQueryTransactionResult{T}"/> with the query results, if any.</returns>
        public static async Task<SingleQueryTransactionResult<T>> QueryAsync<T>(this Transactions transactions, string statement, Action<SingleQueryTransactionConfigBuilder>? configure = null, IScope? scope = null)
        {
            var singleConfig = SingleQueryTransactionConfigBuilder.Create();
            configure?.Invoke(singleConfig);
            return await transactions.QueryAsync<T>(statement, singleConfig, scope).CAF();
        }

        /// <summary>
        /// Run a query in transaction mode.
        /// </summary>
        /// <typeparam name="T">The type of the result.  Use <see cref="object"/> for queries with no results.</typeparam>
        /// <param name="ctx">The AttemptContext to query from.</param>
        /// <param name="statement">The statement to execute.</param>
        /// <param name="configure">An action to configure the options for this query.</param>
        /// <param name="scope">The scope</param>
        /// <returns>A <see cref="SingleQueryTransactionResult{T}"/> with the query results, if any.</returns>
        /// <remarks>IMPORTANT: Any KV operations after this query will be run via the query engine, which has performance implications.</remarks>
        public static async Task<IQueryResult<T>> QueryAsync<T>(this AttemptContext ctx, string statement, Action<TransactionQueryOptions>? configure = null, IScope? scope = null)
        {
            var options = new TransactionQueryOptions();
            configure?.Invoke(options);
            return await ctx.QueryAsync<T>(statement, options, scope).CAF();
        }

        /// <summary>
        /// Configuration builder for values related to Query.
        /// </summary>
        /// <param name="config">The config builder to modify.</param>
        /// <param name="configure">An action to invoke the <see cref="TransactionQueryConfigBuilder"/> to configure query options for transactions.</param>
        /// <returns>The original <see cref="TransactionConfigBuilder"/>.</returns>
        public static TransactionConfigBuilder QueryConfig(this TransactionConfigBuilder config, Action<TransactionQueryConfigBuilder>? configure)
        {
            var queryConfigBuilder = TransactionQueryConfigBuilder.Create();
            configure?.Invoke(queryConfigBuilder);
            config.QueryConfig(queryConfigBuilder);
            return config;
        }
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
#endif
