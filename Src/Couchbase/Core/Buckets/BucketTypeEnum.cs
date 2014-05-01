using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Represents the Bucket types supported by Couchbase Server
    /// </summary>
    internal enum BucketTypeEnum
    {
        /// <summary>
        /// A persistent Bucket supporting replication and rebalancing.
        /// </summary>
        Couchbase = 0x00,
        
        /// <summary>
        /// A Bucket supporting in-memory Key/Value operations.
        /// </summary>
        Memcached = 0x01
    }
}
