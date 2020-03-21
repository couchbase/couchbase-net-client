using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class ClusterProvider : IClusterProvider
    {
        private readonly IOptions<ClusterOptions> _options;
        private readonly ILoggerFactory _loggerFactory;
        private ICluster _cluster;
        private bool _disposed = false;

        public ClusterProvider(IOptions<ClusterOptions> options, ILoggerFactory loggerFactory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public virtual async ValueTask<ICluster> GetClusterAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ClusterProvider));
            }

            if (_cluster != null)
            {
                return _cluster;
            }

            _cluster = await CreateClusterAsync(_options.Value).ConfigureAwait(false);

            return _cluster;
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

                _cluster?.Dispose();
                _cluster = null;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();

            return default;
        }
    }
}
