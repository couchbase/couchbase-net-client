using System.ComponentModel;

namespace Couchbase.Management
{
    public enum ConflictResolutionType
    {
        [Description("lww")]
        Timestamp,

        [Description("seqno")]
        SequenceNumber
    }
}