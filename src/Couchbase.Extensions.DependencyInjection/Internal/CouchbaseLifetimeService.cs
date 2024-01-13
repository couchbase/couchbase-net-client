using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class CouchbaseLifetimeService :  ICouchbaseLifetimeService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string? _serviceKey;

        public CouchbaseLifetimeService(IServiceProvider serviceProvider) : this(serviceProvider, null)
        {
        }

        public CouchbaseLifetimeService(IServiceProvider serviceProvider, [ServiceKey] string? serviceKey)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (serviceProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;
            _serviceKey = serviceKey;
        }

        // Note: Our implementation doesn't require disposing both IBucketProvider and IClusterProvider because
        // they are the same singleton. However, it's possible that one or the other could have been registered
        // with an alternate implementation, so we dispose both just to be safe.

        /// <inheritdoc />
        public void Close()
        {
            _serviceProvider.GetKeyedService<IBucketProvider>(_serviceKey)?.Dispose();
            _serviceProvider.GetKeyedService<IClusterProvider>(_serviceKey)?.Dispose();
        }

        /// <inheritdoc />
        public async ValueTask CloseAsync()
        {
            await (_serviceProvider.GetKeyedService<IBucketProvider>(_serviceKey)?.DisposeAsync() ?? default).ConfigureAwait(false);
            await (_serviceProvider.GetKeyedService<IClusterProvider>(_serviceKey)?.DisposeAsync() ?? default).ConfigureAwait(false);
        }
    }
}
