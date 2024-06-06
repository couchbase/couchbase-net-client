#nullable enable
using System;
using Couchbase.Core.Compatibility;
using Couchbase.KeyValue;
using Couchbase.Query;
using Microsoft.Extensions.Logging;

namespace Couchbase.Integrated.Transactions.Config
{
    /// <summary>
    /// The configuration to use for each transaction against a given cluster.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public record TransactionConfig(
        DurabilityLevel DurabilityLevel = TransactionConfig.DefaultDurabilityLevel,
        TimeSpan? ExpirationTime = null,
        TimeSpan? CleanupWindow = null,
        bool CleanupClientAttempts = true,
        bool CleanupLostAttempts = true,
        QueryScanConsistency? ScanConsistency = null,
        KeySpace? MetadataCollection = null,
        ILoggerFactory? LoggerFactory = null)
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

        public static readonly TimeSpan DefaultExpiration = TimeSpan.FromMilliseconds(DefaultExpirationMilliseconds);

        /// <summary>
        /// The default cleanup window, in milliseconds.
        /// </summary>
        /// <seealso cref="TransactionConfig.CleanupWindow"/>
        public const int DefaultCleanupWindowMilliseconds = 60_000;

        public static readonly TimeSpan DefaultCleanupWindow =
            TimeSpan.FromMilliseconds(DefaultCleanupWindowMilliseconds);

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

        // /// <summary>
        // /// Gets a value indicating whether to run the background thread to clean up lost/abandoned transactions from other clients.
        // /// </summary>
        // public bool CleanupLostAttempts { get; init; }
        //
        // /// <summary>
        // /// Gets a value indicating whether to run the background thread to clean up failed attempts from this transactions instance.
        // /// </summary>
        // public bool CleanupClientAttempts { get; init; }
        //
        // /// <summary>
        // /// Gets a value indicating the period between runs of the cleanup thread.
        // /// </summary>
        // public TimeSpan CleanupWindow { get; init; }
        //
        // /// <summary>
        // /// Gets a value indicating the timeout on Couchbase Key/Value operations.
        // /// </summary>
        // public TimeSpan? KeyValueTimeout { get; init; }
        //
        // /// <summary>
        // /// Gets a <see cref="ILoggerFactory"/> that transactions will use for internal logging.
        // /// </summary>
        // public ILoggerFactory? LoggerFactory { get; init; }
        //
        // /// <summary>
        // /// Gets the <see cref="KeySpace"/> to use for Active Transaction Record metadata.
        // /// </summary>
        // public KeySpace? MetadataCollection { get; init; }
        //
        // /// <summary>
        // /// Gets the <see cref="QueryScanConsistency"/> to use for transaction query operations.
        // /// </summary>
        // public QueryScanConsistency? ScanConsistency { get; init; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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







