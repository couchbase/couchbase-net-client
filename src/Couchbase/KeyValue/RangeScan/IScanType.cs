namespace Couchbase.KeyValue.RangeScan
{
    #nullable enable

    /// <summary>
    /// A marker interface for scan implementations.
    /// </summary>
    public interface IScanType
    {
        /// <summary>
        /// Creates a new KV range scan, scanning between two <see cref="ScanTerm"/> ScanTerms.
        /// </summary>
        /// <param name="from"> From the <see cref="ScanTerm"/> to start scanning from. </param>
        /// <param name="to"> To the <see cref="ScanTerm"/> to scan to.</param>
        /// <returns> A newly created <see cref="RangeScan"/> to be passed into the Collection API.</returns>
        RangeScan? RangeScan(ScanTerm from, ScanTerm to);

        /// <summary>
        /// Creates a new KV range scan, scanning all document IDs starting with the given Prefix.
        /// </summary>
        /// <param name="prefix"> The Prefix <see cref="ScanTerm"/> to start scanning from. </param>
        /// <returns> A newly created <see cref="PrefixScan"/> to be passed into the Collection API.</returns>
        PrefixScan? PrefixScan(string prefix);

        /// <summary>
        /// Creates a new KV sampling scan, which randomly samples documents up until the configured limit with a default seed.
        /// </summary>
        /// <param name="limit"> limit the number of documents to limit sampling to. </param>
        /// <returns> A newly created <see cref="SamplingScan(ulong)"/> to be passed into the Collection API.</returns>
        SamplingScan? SamplingScan(ulong limit);

        /// <summary>
        /// Creates a new KV sampling scan, which randomly samples documents up until the configured limit with a custom seed.
        /// </summary>
        /// <param name="limit"> </param>
        /// <param name="seed"> The custom seed used for sampling.</param>
        /// <returns></returns>
        SamplingScan? SamplingScan(ulong limit, ulong seed);
    }
}
