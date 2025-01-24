using Couchbase.Query;

namespace Couchbase.Client.Transactions.Config
{
    /// <summary>
    /// A configuration builder for Transactional Queries.
    /// </summary>
    public sealed class TransactionQueryConfigBuilder
    {
        internal QueryScanConsistency ScanConsistencyValue { get; set; }

        internal TransactionQueryConfigBuilder()
        {
        }

        /// <summary>
        /// Creates an instance of this class.
        /// </summary>
        /// <returns>A default instance of this class.</returns>
        public static TransactionQueryConfigBuilder Create() => new();

        /// <summary>
        /// Sets the default scan consistency to use for all query statements in the transaction. Default is unset (and query will default to REQUEST_PLUS).
        /// </summary>
        /// <param name="scanConsistency">The QueryScanConsistency to use.</param>
        /// <returns>The config builder.</returns>
        TransactionQueryConfigBuilder ScanConsistency(Query.QueryScanConsistency scanConsistency)
        {
            ScanConsistencyValue = scanConsistency;
            return this;
        }

        /// <summary>
        /// Build an instance of <see cref="TransactionQueryOptions"/>.
        /// </summary>
        /// <returns></returns>
        public TransactionQueryOptions Build()
        {
            var options = new TransactionQueryOptions();
            options.ScanConsistency(ScanConsistencyValue);
            return options;
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
