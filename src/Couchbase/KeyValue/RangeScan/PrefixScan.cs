using Couchbase.Core.Compatibility;
using Couchbase.Utils;

namespace Couchbase.KeyValue.RangeScan;

/// <summary>
/// A PrefixScan
/// </summary>
[InterfaceStability(Level.Volatile)]
public class PrefixScan : RangeScan
{
    public PrefixScan(string prefix) : base(ScanTerm.Inclusive(prefix), ScanTerm.Exclusive(prefix + CouchbaseStrings.MaximumPattern))
    {
    }
}
