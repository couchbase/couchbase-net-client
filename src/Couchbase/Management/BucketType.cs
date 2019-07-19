using System.ComponentModel;

namespace Couchbase.Management
{
    public enum BucketType
    {
        [Description("membase")]
        Couchbase,

        [Description("memcached")]
        Memcached,

        [Description("ephemeral")]
        Ephemeral
    }
}