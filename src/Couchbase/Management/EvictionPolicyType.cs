using System.ComponentModel;

namespace Couchbase.Management
{
    public enum EvictionPolicyType
    {
        [Description("fullEviction")]
        FullEviction,

        [Description("valueOnly")]
        ValueOnly
    }
}