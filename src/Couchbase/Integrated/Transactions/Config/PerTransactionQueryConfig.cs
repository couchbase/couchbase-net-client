#if NET5_0_OR_GREATER
#nullable enable
using Couchbase.Query;

namespace Couchbase.Integrated.Transactions.Config
{
    /// <summary>
    /// Allows setting a per-transaction query configuration.
    /// </summary>
    internal class PerTransactionQueryConfig
    {
        /// <summary>
        /// Gets or sets the index scan consistency for query operations.
        /// </summary>
        public QueryScanConsistency? ScanConsistency { get; set; }
    }
}
#endif
