using System.Collections.Generic;
using System.Net;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a new <see cref="IVBucket"/>.
    /// </summary>
    internal interface IVBucketFactory
    {
        /// <summary>
        /// Creates a new <see cref="IVBucket"/>.
        /// </summary>
        /// <returns>The new <see cref="IVBucket"/>.</returns>
        IVBucket Create(ICollection<IPEndPoint> endPoints, short index, short primary,
            short[] replicas, ulong rev, VBucketServerMap vBucketServerMap, string bucketName);
    }
}
