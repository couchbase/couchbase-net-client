namespace Couchbase.KeyValue.RangeScan
{
    public abstract class ScanType : IScanType
    {
        /// <inheritdoc />
        public RangeScan RangeScan(ScanTerm from, ScanTerm to)
        {
            return new RangeScan(from, to);
        }

        /// <inheritdoc />
        public PrefixScan PrefixScan(string prefix)
        {
            return new PrefixScan(prefix);
        }

        /// <inheritdoc />
        public SamplingScan SamplingScan(ulong limit)
        {
            return new SamplingScan(limit);
        }

        /// <inheritdoc />
        public SamplingScan SamplingScan(ulong limit, ulong seed)
        {
            return new SamplingScan(limit, seed);
        }
    }
}
