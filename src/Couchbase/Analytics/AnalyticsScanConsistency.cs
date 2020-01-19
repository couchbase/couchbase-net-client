using System.ComponentModel;

namespace Couchbase.Analytics
{
    public enum AnalyticsScanConsistency
    {
        /// <summary>
        /// The default which means that the query can return data that is currently indexed
        /// and accessible by the index or the view. The query output can be arbitrarily
        /// out-of-date if there are many pending mutations that have not been indexed by
        /// the index or the view. This consistency level is useful for queries that favor
        /// low latency and do not need precise and most up-to-date information.
        /// </summary>
        [Description("not_bounded")]
        NotBounded,

        /// <summary>
        /// This level provides the strictest consistency level and thus executes with higher
        /// latencies than the other levels. This consistency level requires all mutations, up
        /// to the moment of the query request, to be processed before the query execution can start.
        /// </summary>
        [Description("request_plus")]
        RequestPlus
    }
}
