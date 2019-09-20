using System.ComponentModel;

namespace Couchbase.Management.Buckets
{
    public enum ConflictResolutionType
    {
        [Description("lww")]
        Timestamp,

        [Description("seqno")]
        SequenceNumber
    }
}
