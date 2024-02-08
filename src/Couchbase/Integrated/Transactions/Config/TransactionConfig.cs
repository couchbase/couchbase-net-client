#if NET5_0_OR_GREATER
#nullable enable
using System;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

namespace Couchbase.Integrated.Transactions.Config
{
    /// <summary>
    /// The configuration to use for each transaction against a given cluster.
    /// </summary>
    internal class TransactionConfig
    {
        /// <summary>
        /// The default durability level.
        /// </summary>
        /// <seealso cref="TransactionConfig.DurabilityLevel"/>
        public const DurabilityLevel DefaultDurabilityLevel = DurabilityLevel.Majority;

        /// <summary>
        /// The default expiration, in milliseconds.
        /// </summary>
        /// <seealso cref="TransactionConfig.ExpirationTime"/>
        public const int DefaultExpirationMilliseconds = 15_000;

        /// <summary>
        /// The default cleanup window, in milliseconds.
        /// </summary>
        /// <seealso cref="TransactionConfig.CleanupWindow"/>
        public const int DefaultCleanupWindowMilliseconds = 60_000;

        /// <summary>
        /// The default value of <see cref="TransactionConfig.CleanupLostAttempts"/> (true).
        /// </summary>
        public const bool DefaultCleanupLostAttempts = true;

        /// <summary>
        /// The default value of <see cref="CleanupClientAttempts"/> (true).
        /// </summary>
        public const bool DefaultCleanupClientAttempts = true;

        /// <summary>
        /// The default log level for failures.
        /// </summary>
        public const Severity DefaultLogOnFailure = Severity.Error;

        /// <summary>
        /// Gets a value indicating the time before a transaction expires and no more attempts will be made.
        /// </summary>
        public TimeSpan ExpirationTime { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether to run the background thread to clean up lost/abandoned transactions from other clients.
        /// </summary>
        public bool CleanupLostAttempts { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether to run the background thread to clean up failed attempts from this transactions instance.
        /// </summary>
        public bool CleanupClientAttempts { get; internal set; }

        /// <summary>
        /// Gets a value indicating the period between runs of the cleanup thread.
        /// </summary>
        public TimeSpan CleanupWindow { get; internal set; }

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
        /// Gets the <see cref="ICouchbaseCollection"/> to use for Active Transaction Record metadata.
        /// </summary>
        public ICouchbaseCollection? MetadataCollection { get; internal set; }

        /// <summary>
        /// Gets the <see cref="QueryScanConsistency"/> to use for transaction query operations.
        /// </summary>
        public QueryScanConsistency? ScanConsistency { get; internal set; }

        internal TransactionConfig(
            DurabilityLevel durabilityLevel = DefaultDurabilityLevel,
            TimeSpan? expirationTime = null,
            TimeSpan? cleanupWindow = null,
            TimeSpan? keyValueTimeout = null,
            bool cleanupClientAttempts = DefaultCleanupClientAttempts,
            bool cleanupLostAttempts = DefaultCleanupLostAttempts,
            QueryScanConsistency? scanConsistency = null
        )
        {
            ExpirationTime = expirationTime ?? TimeSpan.FromMilliseconds(DefaultExpirationMilliseconds);
            CleanupLostAttempts = cleanupLostAttempts;
            CleanupClientAttempts = cleanupClientAttempts;
            CleanupWindow = cleanupWindow ?? TimeSpan.FromMilliseconds(DefaultCleanupWindowMilliseconds);
            KeyValueTimeout = keyValueTimeout;
            DurabilityLevel = durabilityLevel;
            ScanConsistency = scanConsistency;
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
