using System.ComponentModel;
using Couchbase.Core.Compatibility;

namespace Couchbase.Management.Buckets
{
    /// <summary>
    /// The type of storage to use with the bucket. This is only specified for "couchbase" buckets.
    /// </summary>
    [InterfaceStability(Level.Uncommitted)]
    public enum StorageBackend
    {
        /// <summary>
        /// Backend storage type 'couchstore'.
        /// </summary>
        [Description("couchstore")]
        Couchstore,

        /// <summary>
        /// Backend storage type 'magma'.
        /// </summary>
        [Description("magma")]
        Magma
    }
}
