using Couchbase.Core;

namespace Couchbase
{
    /// <summary>
    /// Provides access to <see cref="IBucket"/> instances.
    /// </summary>
    public interface IBucketCache
    {
        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="IBucketCache"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for an <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <returns>An <see cref="IBucket"/> instance.</returns>
        IBucket Get(string bucketName);

        /// <summary>
        /// Opens or gets an <see cref="IBucket"/> instance from the <see cref="ICluster"/> that this <see cref="IBucketCache"/> is wrapping.
        /// The <see cref="IBucket"/> will be cached and subsequent requests for an <see cref="IBucket"/> of the same name will return the
        /// cached instance.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to open or get.</param>
        /// <param name="password">Bucket password, or null for unsecured buckets.</param>
        /// <returns>An <see cref="IBucket"/> instance.</returns>
        IBucket Get(string bucketName, string password);

        /// <summary>
        /// Removes the given bucket from the cached buckets.
        /// </summary>
        /// <param name="bucketName">The name of the <see cref="IBucket"/> to remove.</param>
        void Remove(string bucketName);
    }
}
