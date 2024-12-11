#nullable enable
using System;
using Couchbase.Core.Compatibility;
using Couchbase.KeyValue;
using Couchbase.Query;

namespace Couchbase.Client.Transactions.Config
{
    /// <summary>
    /// A record representing a config applied to a single transaction.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public record PerTransactionConfig
    {
        /// <summary>
        /// Gets an optional value indicating the minimum durability level desired for this transaction.
        /// </summary>
        public DurabilityLevel? DurabilityLevel { get; init; }

        /// <summary>
        /// Gets an optional value indicating the relative expiration time of the transaction for this transaction.
        /// </summary>
        public TimeSpan? Timeout { get; init; }


        /// <summary>
        /// Gets an option value indicating the timeout on Couchbase Key/Value operations for this transaction.
        /// </summary>
        public TimeSpan? KeyValueTimeout { get; init; }

        /// <summary>
        /// The scan consistency to use for query operations (default: RequestPlus)
        /// </summary>
        public QueryScanConsistency? ScanConsistency { get; init; }
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







