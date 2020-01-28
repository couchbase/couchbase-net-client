using System;
using Couchbase.Management.Buckets;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a <seealso cref="BucketBase"/> based on <seealso cref="BucketType"/>.
    /// </summary>
    internal interface IBucketFactory
    {
        /// <summary>
        /// Creates a <seealso cref="BucketBase"/> based on <seealso cref="BucketType"/>.
        /// </summary>
        /// <param name="name">Name of the bucket.</param>
        /// <param name="bucketType">Type of the bucket.</param>
        /// <returns>Correct bucket implementation.</returns>
        BucketBase Create(string name, BucketType bucketType);
    }
}
