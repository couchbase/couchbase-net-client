namespace Couchbase.KeyValue.RangeScan
{
    /// <summary>
    /// The sort direction for KV Range scans.
    /// </summary>
    public enum ScanSort
    {
        /// <summary>
        /// No sorting is used.
        /// </summary>
        None,

        /// <summary>
        /// The keys are in ascending order.
        /// </summary>
        Ascending
    }
}
