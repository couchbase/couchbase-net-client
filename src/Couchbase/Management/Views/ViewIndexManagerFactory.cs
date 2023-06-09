using System;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Management.Views
{
    /// <inheritdoc />
    internal class ViewIndexManagerFactory : IViewIndexManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ViewIndexManagerFactory(IServiceProvider serviceProvider)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (serviceProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public IViewIndexManager Create(string bucketName) =>
            new ViewIndexManager(bucketName,
                _serviceProvider.GetRequiredService<IServiceUriProvider>(),
                _serviceProvider.GetRequiredService<ICouchbaseHttpClientFactory>(),
                _serviceProvider.GetRequiredService<ILogger<ViewIndexManager>>(),
                _serviceProvider.GetRequiredService<IRedactor>());
    }
}
