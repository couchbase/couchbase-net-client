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

            // We're wrapping the Task in a ValueTask here for future-proofing, this will allow us to change the
            // implementation in the future to one with fewer allocations.
            return new ValueTask<IBucket>(
                _buckets.GetOrAdd(bucketName, async name =>
                {
                    var cluster = await _clusterProvider.GetClusterAsync().ConfigureAwait(false);

                    return await cluster.BucketAsync(name).ConfigureAwait(false);
                }));
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
