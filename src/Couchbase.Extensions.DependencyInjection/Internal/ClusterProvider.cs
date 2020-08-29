using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class ClusterProvider : IClusterProvider
    {
        private readonly ILoggerFactory _loggerFactory;
        private AsyncLazy<ICluster> _cluster;
        private bool _disposed = false;

        public ClusterProvider(IOptions<ClusterOptions> options, ILoggerFactory loggerFactory)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

            _cluster = new AsyncLazy<ICluster>(() => CreateClusterAsync(options.Value));
        }

        public virtual ValueTask<ICluster> GetClusterAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ClusterProvider));
            }

            return new ValueTask<ICluster>(_cluster.Value);
        }

        /// <summary>
        /// Seam for injecting mock.
        /// </summary>
        protected virtual Task<ICluster> CreateClusterAsync(ClusterOptions clusterOptions)
        {
            clusterOptions.WithLogging(_loggerFactory);

            return Cluster.ConnectAsync(clusterOptions);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_cluster?.IsValueCreated ?? false)
                {
                    try
                    {
                        _cluster?.GetAwaiter().GetResult().Dispose();
                    }
                    catch
                    {
                        // Eat any exception that was thrown during cluster creation
                    }

                    _cluster = null;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_cluster?.IsValueCreated ?? false)
                {
                    var cluster = await _cluster.Value.ConfigureAwait(false);

                    await cluster.DisposeAsync().ConfigureAwait(false);

                    _cluster = null;
                }
            }
        }
    }
}
