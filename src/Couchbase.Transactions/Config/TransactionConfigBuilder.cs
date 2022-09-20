using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.Transactions.Config
{
    /// <summary>
    /// A class for configuring transactions options.
    /// </summary>
    public class TransactionConfigBuilder
    {
        private readonly TransactionConfig _config;
        private TransactionQueryConfigBuilder? _queryConfigBuilder = null;

        private TransactionConfigBuilder()
        {
            _config = new TransactionConfig();
        }

        /// <summary>
        /// Create an instance of the config.
        /// </summary>
        /// <returns>An instance of the <see cref="TransactionConfigBuilder"/>.</returns>
        public static TransactionConfigBuilder Create() => new TransactionConfigBuilder();

        /// <summary>
        /// Set the <see cref="TransactionConfig.ExpirationTime"/> value.
        /// </summary>
        /// <param name="expirationTime">The maximum time that transactions created by this Transactions object can run for, before expiring.</param>
        /// <returns>The builder.</returns>
        public TransactionConfigBuilder ExpirationTime(TimeSpan expirationTime)
        {
            _config.ExpirationTime = expirationTime;
            return this;
        }

        /// <summary>
        /// The writes of all transactions created by this object will be performed with this durability setting.
        /// </summary>
        /// <param name="durabilityLevel">A value from the <see cref="DurabilityLevel(KeyValue.DurabilityLevel)"/> enum.</param>
        /// <returns>The builder.</returns>
        public TransactionConfigBuilder DurabilityLevel(DurabilityLevel durabilityLevel)
        {
            _config.DurabilityLevel = durabilityLevel;
            return this;
        }

        /// <summary>
        /// Set the default timeout used for all KV writes.
        /// </summary>
        /// <param name="keyValueTimeout">The default timeout used for all KV writes.</param>
        /// <returns>The builder.</returns>
        public TransactionConfigBuilder KeyValueTimeout(TimeSpan keyValueTimeout)
        {
            _config.KeyValueTimeout = keyValueTimeout;
            return this;
        }

        /// <summary>
        /// Each client that has cleanupLostAttempts(true) enabled, will be participating in the distributed cleanup process.
        /// This involves checking all ATRs every cleanup window, and this parameter controls the length of that window.
        /// </summary>
        /// <param name="cleanupWindow">The length of the cleanup window.</param>
        /// <returns>The builder.</returns>
        public TransactionConfigBuilder CleanupWindow(TimeSpan cleanupWindow)
        {
            _config.CleanupWindow = cleanupWindow;
            return this;
        }

        /// <summary>
        /// Controls where any transaction attempts made by this client are automatically removed.
        /// </summary>
        /// <param name="cleanupClientAttempts">Whether to cleanup attempts made by this client.</param>
        /// <returns>The builder.</returns>
        public TransactionConfigBuilder CleanupClientAttempts(bool cleanupClientAttempts)
        {
            _config.CleanupClientAttempts = cleanupClientAttempts;
            return this;
        }

        /// <summary>
        /// Controls where a background process is created to cleanup any 'lost' transaction attempts.
        /// </summary>
        /// <param name="cleanupLostAttempts">Whether to cleanup lost attempts from other clients.</param>
        /// <returns>The builder.</returns>
        public TransactionConfigBuilder CleanupLostAttempts(bool cleanupLostAttempts)
        {
            _config.CleanupLostAttempts = cleanupLostAttempts;
            return this;
        }

        /// <summary>
        /// Generate a <see cref="TransactionConfig"/> from the values provided.
        /// </summary>
        /// <returns>A <see cref="TransactionConfig"/> that has been initialized with the given values.</returns>
        public TransactionConfig Build() => _config;

        /// <summary>
        /// The <see cref="Microsoft.Extensions.Logging.ILoggerFactory"/> to be used for logging in the Transactions internals.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <returns>The builder.</returns>
        public TransactionConfigBuilder LoggerFactory(ILoggerFactory loggerFactory)
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
        public TransactionConfigBuilder MetadataCollection(ICouchbaseCollection metadataCollection)
        {
            _config.MetadataCollection = metadataCollection;
            return this;
        }

        /// <summary>
        /// Configuration builder for values related to Query.
        /// </summary>
        /// <param name="queryConfigBuilder">A <see cref="TransactionQueryConfigBuilder"/> to configure query options for transactions.</param>
        /// <returns>The original <see cref="TransactionConfigBuilder"/>.</returns>
        public TransactionConfigBuilder QueryConfig(TransactionQueryConfigBuilder queryConfigBuilder)
        {
            _config.ScanConsistency = queryConfigBuilder.ScanConsistencyValue;
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
