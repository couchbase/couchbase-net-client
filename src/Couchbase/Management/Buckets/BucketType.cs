using System.ComponentModel;

namespace Couchbase.Management.Buckets
{
    /// <summary>
    /// Represents the Bucket types supported by Couchbase Server
    /// </summary>
    public enum BucketType
    {
        /// <summary>
        /// A persistent Bucket supporting replication and rebalancing.
        /// </summary>
        [Description("membase")]
        Couchbase,

        /// <summary>
        /// A Bucket supporting in-memory Key/Value operations.
        /// </summary>
        [Description("memcached")]
        Memcached,

        /// <summary>
        /// The ephemeral bucket type for in-memory buckets with Couchbase bucket behavior.
        /// </summary>
        [Description("ephemeral")]
        Ephemeral
    }
}
