using System;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
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
        public ICouchbaseCollectionManager Create(string bucketName) =>
            new CollectionManager(bucketName,
                _serviceProvider.GetRequiredService<IServiceUriProvider>(),
                _serviceProvider.GetRequiredService<ICouchbaseHttpClientFactory>(),
                _serviceProvider.GetRequiredService<ILogger<CollectionManager>>(),
                _serviceProvider.GetRequiredService<IRedactor>());
    }
}
