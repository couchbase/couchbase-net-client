using Couchbase.Query;
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Transactions.Config
{
    /// <summary>
    /// Allows setting a per-transaction query configuration.
    /// </summary>
    public class PerTransactionQueryConfig
    {
        /// <summary>
        /// Gets or sets the index scan consistency for query operations.
        /// </summary>
        public QueryScanConsistency? ScanConsistency { get; set; }
    }
}
