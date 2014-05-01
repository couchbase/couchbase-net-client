using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Specifies the type of NodeLocator that a Couchbase Bucket uses.
    /// </summary>
    internal enum NodeLocatorEnum
    {
        /// <summary>
        /// Used for persistent Couchbase Buckets.
        /// </summary>
        VBucket,

        /// <summary>
        /// Used for in-memory Memcached Buckets.
        /// </summary>
        Ketama
    }
}
