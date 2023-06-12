using System;
using Couchbase.KeyValue;

namespace Couchbase.Transactions.Config
{
    /// <summary>
    /// A subset Transaction Configuration options relevant to single-query transactions.
    /// </summary>
    public class SingleQueryTransactionConfigBuilder
    {
        internal SingleQueryTransactionConfigBuilder()
        {
        }

        /// <summary>
        /// Create a default SingleQueryTransactionConfigBuilder.
        /// </summary>
        /// <returns>A default instance of this type.</returns>
        public static SingleQueryTransactionConfigBuilder Create() => new();

        /// <summary>
        /// Gets or sets the query options for this single query transaction.
        /// </summary>
        public TransactionQueryOptions QueryOptionsValue { get; internal set; } = new();

        /// <summary>
        /// Configure the query-specific options for this transaction.
        /// </summary>
        /// <param name="configure">An Action to configure the query options.</param>
        /// <returns>The builder.</returns>
        public SingleQueryTransactionConfigBuilder QueryOptions(Action<TransactionQueryOptions> configure)
        {
            if (configure != null)
            {
                configure(QueryOptionsValue);
            }

            return this;
        }

        /// <summary>
        /// Set the query-specific options for this transaction.
        /// </summary>
        /// <param name="options">The transaction query options to use.</param>
        /// <returns>The builder.</returns>
        public SingleQueryTransactionConfigBuilder QueryOptions(TransactionQueryOptions options)
        {
            QueryOptionsValue = options;
            return this;
        }


        /// <summary>
        /// Gets or sets the Durability Level for this single query transaction.
        /// </summary>
        public DurabilityLevel? DurabilityLevelValue { get; internal set; } = null;

        /// <summary>
        /// Set the durability for this transaction.
        /// </summary>
        /// <param name="durability">The durability level to use.</param>
        /// <returns>The builder.</returns>
        public SingleQueryTransactionConfigBuilder DurabilityLevel(DurabilityLevel durability)
        {
            DurabilityLevelValue = durability;
            return this;
        }

        /// <summary>
        /// Gets or sets the relative expiration time for this single query transaction.
        /// </summary>
        public TimeSpan? TimeoutValue { get; internal set; } = null;

        /// <summary>
        /// Set the timeout of this transaction.
        /// </summary>
        /// <param name="timeout">The expiration time, relative to now.</param>
        /// <returns>The builder.</returns>
        public SingleQueryTransactionConfigBuilder Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        internal PerTransactionConfig Build()
        {
            // Build a TransactionConfig, setting only the values that have been set to non-default.
            var config = new PerTransactionConfig();
            config.Timeout = this.TimeoutValue ?? config.Timeout;
            config.DurabilityLevel = this.DurabilityLevelValue ?? config.DurabilityLevel;
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
