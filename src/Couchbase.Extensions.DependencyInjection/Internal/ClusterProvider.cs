using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    [RequiresUnreferencedCode(ServiceCollectionExtensions.RequiresUnreferencedCodeWarning)]
    [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
    internal class ClusterProvider : IClusterProvider, IBucketProvider
    {
        private AsyncLazy<ICluster>? _cluster;
        private readonly ConcurrentDictionary<string, Task<IBucket>> _buckets = new();

        public ClusterProvider(IOptionsMonitor<ClusterOptions> options)
            : this(options, serviceKey: null)
        {
        }

        public ClusterProvider(IOptionsMonitor<ClusterOptions> options, [ServiceKey] string? serviceKey)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (options == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(options));
            }

            _cluster = new AsyncLazy<ICluster>(() => CreateClusterAsync(options.Get(serviceKey ?? Options.DefaultName)));
        }

        public virtual ValueTask<ICluster> GetClusterAsync()
        {
            EnsureNotDisposed();

            return new ValueTask<ICluster>(_cluster!.Value);
        }

        public ValueTask<ICluster> GetClusterAsync(CancellationToken cancellationToken)
        {
            // Don't cancel the cluster connection attempt if the supplied token is canceled, since the cluster
            // is shared. Instead, just cancel the wait for the result.

            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<ICluster>(cancellationToken);
            }

            var result = GetClusterAsync();
            if (result.IsCompleted)
            {
                return result;
            }

            return new ValueTask<ICluster>(result.AsTask().WaitAsync(cancellationToken));
        }

        /// <summary>
        /// Seam for injecting mock.
        /// </summary>
        protected virtual Task<ICluster> CreateClusterAsync(ClusterOptions clusterOptions)
        {
            return Cluster.ConnectAsync(clusterOptions);
        }

        /// <inheritdoc />
        public ValueTask<IBucket> GetBucketAsync(string bucketName)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (bucketName == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketName));
            }
            EnsureNotDisposed();

            // Note: The implementation below may call GetBucketFromClusterAsync more than
            // once simultaneously. This is okay because the local cache is purely a performance
            // optimization, the backing implementation from the SDK is already thread-safe.

            Task<IBucket> task = _buckets.GetOrAdd(bucketName, GetBucketFromClusterAsync);

            if (task.IsFaulted)
            {
                // If the previous attempt is already complete and it failed with an exception,
                // go back to the cluster and try to get the bucket again. This should be a cold path
                // once we're successfully bootstrapped. Note that multiple simultaneous requests
                // to this method will still return the same task and then they will all fail if bootstrap
                // fails, this path is only starts up a new bootstrap on the next request.

                task = _buckets[bucketName] = GetBucketFromClusterAsync(bucketName);
            }

            // We're wrapping the Task in a ValueTask here for future-proofing, this will allow us to change the
            // implementation in the future to one with fewer allocations.
            return new ValueTask<IBucket>(task);
        }

        private async Task<IBucket> GetBucketFromClusterAsync(string bucketName)
        {
            var cluster = await GetClusterAsync().ConfigureAwait(false);

            return await cluster.BucketAsync(bucketName).ConfigureAwait(false);
        }

        public void Dispose()
        {
            var lazyCluster = Interlocked.Exchange(ref _cluster, null);
            if (lazyCluster is not null && lazyCluster.IsValueCreated)
            {
                foreach (var bucket in GetAndClearBuckets())
                {
                    bucket.Dispose();
                }

                try
                {
                    lazyCluster.Value.GetAwaiter().GetResult().Dispose();
                }
                catch
                {
                    // Eat any exception that was thrown during cluster creation
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            var lazyCluster = Interlocked.Exchange(ref _cluster, null);
            if (lazyCluster is not null && lazyCluster.IsValueCreated)
            {
                foreach (var bucket in GetAndClearBuckets())
                {
                    await bucket.DisposeAsync().ConfigureAwait(false);
                }

                var cluster = await lazyCluster.Value.ConfigureAwait(false);

                await cluster.DisposeAsync().ConfigureAwait(false);
            }
        }

        [MemberNotNull(nameof(_cluster))]
        private void EnsureNotDisposed()
        {
            if (_cluster is null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(ClusterProvider));
            }
        }

        private IEnumerable<IBucket> GetAndClearBuckets()
        {
            var bucketCache = _buckets.Values.ToList();
            _buckets.Clear();

            return bucketCache.Where(p => p is { IsCompleted: true, IsFaulted: false, IsCanceled: false })
                .Select(p => p.Result);
        }
    }
}
