using System;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Management.Collections
{
    /// <inheritdoc />
    internal class CollectionManagerFactory : ICollectionManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CollectionManagerFactory(IServiceProvider serviceProvider)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (serviceProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public ICouchbaseCollectionManager Create(string bucketName, BucketConfig bucketConfig) =>
            new CollectionManager(bucketName, bucketConfig,
                CouchbaseServiceProviderExtensions.GetRequiredService<IServiceUriProvider>(_serviceProvider),
                CouchbaseServiceProviderExtensions.GetRequiredService<ICouchbaseHttpClientFactory>(_serviceProvider),
                CouchbaseServiceProviderExtensions.GetRequiredService<ILogger<CollectionManager>>(_serviceProvider),
                CouchbaseServiceProviderExtensions.GetRequiredService<IRedactor>(_serviceProvider),
                ServiceProviderServiceExtensions.GetRequiredService<IAppTelemetryCollector>(_serviceProvider));
    }
}
