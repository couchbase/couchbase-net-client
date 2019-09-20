using System.ComponentModel;

namespace Couchbase.Management.Buckets
{
    public enum CompressionMode
    {
        [Description("off")]
        Off,

        [Description("passive")]
        Passive,

        [Description("active")]
        Active
    }
}
