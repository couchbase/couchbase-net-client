using System;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Config
{
    /// <summary>
    /// A class for configuring transactions options.
    /// </summary>
    public class TransactionsConfigBuilder
    {
        private readonly TransactionsConfig _config;

        private TransactionsConfigBuilder()
        {
            _config = new TransactionsConfig();
        }

        /// <summary>
        /// Create an instance of the config.
        /// </summary>
        /// <returns>An instance of the <see cref="TransactionsConfigBuilder"/>.</returns>
        public static TransactionsConfigBuilder Create() => new();

        /// <summary>
        /// Set the <see cref="TransactionsConfig.ExpirationTime"/> value.
        /// </summary>
        /// <param name="expirationTime">The maximum time that transactions created by this Transactions object can run for, before expiring.</param>
        /// <returns>The builder.</returns>
        public TransactionsConfigBuilder ExpirationTime(TimeSpan expirationTime)
        {
            _config.ExpirationTime = expirationTime;
            return this;
        }

        /// <summary>
        /// The writes of all transactions created by this object will be performed with this durability setting.
        /// </summary>
        /// <param name="durabilityLevel">A value from the <see cref="DurabilityLevel(KeyValue.DurabilityLevel)"/> enum.</param>
        /// <returns>The builder.</returns>
        public TransactionsConfigBuilder DurabilityLevel(DurabilityLevel durabilityLevel)
        {
            _config.DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// Set the default timeout used for all KV writes.
        /// </summary>
        /// <param name="keyValueTimeout">The default timeout used for all KV writes.</param>
        /// <returns>The builder.</returns>
        public TransactionsConfigBuilder KeyValueTimeout(TimeSpan keyValueTimeout)
        {
            _config.KeyValueTimeout = keyValueTimeout;
            return this;
        }

        /// <summary>
        /// Generate a <see cref="TransactionsConfig"/> from the values provided.
        /// </summary>
        /// <returns>A <see cref="TransactionsConfig"/> that has been initialized with the given values.</returns>
        public TransactionsConfig Build() => _config;

        /// <summary>
        /// The <see cref="Microsoft.Extensions.Logging.ILoggerFactory"/> to be used for logging in the Transactions internals.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <returns>The builder.</returns>
        public TransactionsConfigBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            _config.LoggerFactory = loggerFactory;
            return this;
        }

        /// <summary>
        /// Set <see cref="ICouchbaseCollection"/> to use for Active Transaction Record metadata.
        /// </summary>
        /// <param name="metadataCollection">The collection to use.</param>
        /// <returns>The builder.</returns>
        /// <remarks>If this is not set, then the metadata collection will be chosen based on the VBucket of the first document modification in the transaction.</remarks>
        public TransactionsConfigBuilder MetadataCollection(Keyspace metadataCollection)
        {
            _config.MetadataCollection = metadataCollection;
            return this;
        }

        /// <summary>
        /// Configuration builder for values related to Query.
        /// </summary>
        /// <param name="queryConfigBuilder">A <see cref="TransactionQueryConfigBuilder"/> to configure query options for transactions.</param>
        /// <returns>The original <see cref="TransactionsConfigBuilder"/>.</returns>
        public TransactionsConfigBuilder QueryConfig(TransactionQueryConfigBuilder queryConfigBuilder)
        {
            _config.ScanConsistency = queryConfigBuilder.ScanConsistencyValue;
            return this;
        }

        /// <summary>
        /// Set various parameters controlling how cleanup of lost/abandoned transactions will function.
        /// </summary>
        /// <param name="cleanupConfig"></param> The <see cref="TransactionCleanupConfig"/> to be used to clean up lost/abandoned transaction
        /// <returns></returns>
        public TransactionsConfigBuilder CleanupConfig(TransactionCleanupConfig cleanupConfig)
        {
            _config.CleanupConfig = cleanupConfig;
            return this;
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
