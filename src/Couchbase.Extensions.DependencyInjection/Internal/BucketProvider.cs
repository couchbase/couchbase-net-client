using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class BucketProvider : IBucketProvider
    {
        private readonly IClusterProvider _clusterProvider;
        private readonly ConcurrentDictionary<string, Task<IBucket>> _buckets = new ConcurrentDictionary<string, Task<IBucket>>();
        private bool _disposed;

        public BucketProvider(IClusterProvider clusterProvider)
        {
            _clusterProvider = clusterProvider ?? throw new ArgumentNullException(nameof(clusterProvider));
        }

        /// <inheritdoc />
        public ValueTask<IBucket> GetBucketAsync(string bucketName)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BucketProvider));
            }
            if (bucketName == null)
            {
                throw new ArgumentNullException(nameof(bucketName));
            }

            // Note: The implementation below may call GetBucketFromClusterAsync more than
            // once simultaneously. This is okay because the local cache is purely a performance
            // optimization, the backing implementation from the SDK is already thread-safe.

            #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            // Use the new overload to reduce heap allocations when available
            var task = _buckets.GetOrAdd(bucketName, GetBucketFromClusterAsyncDelegate, _clusterProvider);
            #else
            var task = _buckets.GetOrAdd(bucketName, name => GetBucketFromClusterAsync(name, _clusterProvider));
            #endif

            if (task.IsFaulted)
            {
                // If the previous attempt is already complete and it failed with an exception,
                // go back to the cluster and try to get the bucket again. This should be a cold path
                // once we're successfully bootstrapped. Note that multiple simultaneous requests
                // to this method will still return the same task and then they will all fail if bootstrap
                // fails, this path is only starts up a new bootstrap on the next request.

                task = _buckets[bucketName] = GetBucketFromClusterAsync(bucketName, _clusterProvider);
            }

            // We're wrapping the Task in a ValueTask here for future-proofing, this will allow us to change the
            // implementation in the future to one with fewer allocations.
            return new ValueTask<IBucket>(task);
        }

        #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        // Cache the delegate to reduce heap allocations
        private static readonly Func<string, IClusterProvider, Task<IBucket>> GetBucketFromClusterAsyncDelegate =
            GetBucketFromClusterAsync;
        #endif

        private static async Task<IBucket> GetBucketFromClusterAsync(string bucketName, IClusterProvider clusterProvider)
        {
            var cluster = await clusterProvider.GetClusterAsync().ConfigureAwait(false);

            return await cluster.BucketAsync(bucketName).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                var bucketCache = _buckets.Values.ToList();
                _buckets.Clear();

                foreach (var bucket in bucketCache.Where(p => p.IsCompleted && !p.IsFaulted && !p.IsCanceled).Select(p => p.Result))
                {
                    bucket.Dispose();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                var bucketCache = _buckets.Values.ToList();
                _buckets.Clear();

                foreach (var bucket in bucketCache.Where(p => p.IsCompleted && !p.IsFaulted && !p.IsCanceled).Select(p => p.Result))
                {
                    await bucket.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
