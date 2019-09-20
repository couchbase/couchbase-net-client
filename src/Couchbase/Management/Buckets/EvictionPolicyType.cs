using System.ComponentModel;

namespace Couchbase.Management.Buckets
{
    public enum EvictionPolicyType
    {
        [Description("fullEviction")]
        FullEviction,

        [Description("valueOnly")]
        ValueOnly
    }
}
