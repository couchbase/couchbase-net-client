namespace Couchbase.KeyValue.RangeScan;

/// <summary>
/// A PrefixScan
/// </summary>
public class PrefixScan : RangeScan
{
    public PrefixScan(string prefix) : base(ScanTerm.Inclusive(prefix), ScanTerm.Exclusive(prefix + ScanTerm.Maximum().Id))
    {

    }
}
