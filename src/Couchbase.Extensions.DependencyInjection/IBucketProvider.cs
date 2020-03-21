using System;
using System.Threading.Tasks;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides access to buckets for a Couchbase cluster.  Should maintain
    /// singleton instances of each bucket.  Consumers should not dispose the
    /// <see cref="IBucket"/> implementations.  Instead, this provider should be
    /// disposed during application shutdown using <see cref="ICouchbaseLifetimeService"/>.
    /// </summary>
    public interface IBucketProvider : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Get a Couchbase bucket.
        /// </summary>
        /// <param name="bucketName">Name of the bucket.</param>
        /// <returns><see cref="IBucket"/> implementation for the given bucket name.</returns>
        ValueTask<IBucket> GetBucketAsync(string bucketName);
    }
}
