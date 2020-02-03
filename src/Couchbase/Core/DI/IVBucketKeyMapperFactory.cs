using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates an <see cref="IKeyMapper"/> for VBuckets.
    /// </summary>
    internal interface IVBucketKeyMapperFactory
    {
        /// <summary>
        /// Creates an <see cref="IKeyMapper"/> for VBuckets.
        /// </summary>
        /// <param name="bucketConfig">Bucket configuration.</param>
        /// <remarks>The new <see cref="IKeyMapper"/>.</remarks>
        IKeyMapper Create(BucketConfig bucketConfig);
    }
}
