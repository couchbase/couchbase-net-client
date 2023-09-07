using Couchbase.Core.Configuration.Server;

namespace Couchbase.Management.Collections
{
    /// <summary>
    /// Creates an <see cref="ICouchbaseCollectionManager"/> for a given bucket.
    /// </summary>
    internal interface ICollectionManagerFactory
    {
        /// <summary>
        /// Creates an <see cref="ICouchbaseCollectionManager"/> for a given bucket.
        /// </summary>
        /// <param name="bucketName">Name of the bucket.</param>
        /// <param name="bucketConfig">The bucket's config.</param>
        /// <returns>The <see cref="ICouchbaseCollectionManager"/>.</returns>
        public ICouchbaseCollectionManager Create(string bucketName, BucketConfig bucketConfig);
    }
}
