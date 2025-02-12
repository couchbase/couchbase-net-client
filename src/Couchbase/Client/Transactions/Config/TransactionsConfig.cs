#nullable enable
using System;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Config
{
    /// <summary>
    /// The configuration to use for each transaction against a given cluster.
    /// </summary>
    public class TransactionsConfig
    {
        /// <summary>
        /// The default durability level.
        /// </summary>
        /// <seealso cref="TransactionsConfig.DurabilityLevel"/>
        public const DurabilityLevel DefaultDurabilityLevel = DurabilityLevel.Majority;

        /// <summary>
        /// The default expiration, in milliseconds.
        /// </summary>
        /// <seealso cref="TransactionsConfig.ExpirationTime"/>
        public const int DefaultExpirationMilliseconds = 15_000;

        /// <summary>
        /// The default log level for failures.
        /// </summary>
        public const Severity DefaultLogOnFailure = Severity.Error;

        /// <summary>
        /// Gets a value indicating the time before a transaction expires and no more attempts will be made.
        /// </summary>
        public TimeSpan ExpirationTime { get; internal set; }

        /// <summary>
        /// Gets a value indicating the timeout on Couchbase Key/Value operations.
        /// </summary>
        public TimeSpan? KeyValueTimeout { get; internal set; }

        /// <summary>
        /// Gets a value for the minimum durability level to use for modification operations.
        /// </summary>
        public DurabilityLevel DurabilityLevel { get; internal set; }

        /// <summary>
        /// Gets a <see cref="ILoggerFactory"/> that transactions will use for internal logging.
        /// </summary>
        public ILoggerFactory? LoggerFactory { get; internal set; }

        /// <summary>
        /// Gets the <see cref="Keyspace"/> to use for Active Transaction Record metadata.
        /// If null, then we put the record in the collection of the first document that gets modified,
        /// removed or inserted in the transaction.
        /// </summary>
        public Keyspace? MetadataCollection { get; internal set; }

        /// <summary>
        /// Gets the <see cref="QueryScanConsistency"/> to use for transaction query operations.
        /// </summary>
        public QueryScanConsistency? ScanConsistency { get; internal set; }

        /// <summary>
        /// Gets the <see cref="TransactionCleanupConfig" /> which is used to configure the cleanup.
        /// </summary>
        public TransactionCleanupConfig CleanupConfig { get; internal set; }

        internal TransactionsConfig(
            DurabilityLevel durabilityLevel = DefaultDurabilityLevel,
            TimeSpan? expirationTime = null,
            TimeSpan? keyValueTimeout = null,
            QueryScanConsistency? scanConsistency = null,
            Keyspace? metadataCollection = null,
            TransactionCleanupConfig? cleanupConfig = null
        )
        {
            ExpirationTime = expirationTime ?? TimeSpan.FromMilliseconds(DefaultExpirationMilliseconds);
            KeyValueTimeout = keyValueTimeout;
            DurabilityLevel = durabilityLevel;
            ScanConsistency = scanConsistency;
            MetadataCollection = metadataCollection;
            CleanupConfig = cleanupConfig ?? new TransactionCleanupConfig();
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
