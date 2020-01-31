using System;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.IO.HTTP;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IHttpStreamingConfigListenerFactory"/>.
    /// </summary>
    internal class HttpStreamingConfigListenerFactory : IHttpStreamingConfigListenerFactory
    {
        private readonly ClusterOptions _clusterOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HttpStreamingConfigListener> _logger;

        public HttpStreamingConfigListenerFactory(ClusterOptions clusterOptions, IServiceProvider serviceProvider, ILogger<HttpStreamingConfigListener> logger)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public HttpStreamingConfigListener Create(string bucketName, IConfigHandler configHandler) =>
            new HttpStreamingConfigListener(bucketName, _clusterOptions,
                _serviceProvider.GetRequiredService<CouchbaseHttpClient>(), // Get each time so it's not a singleton
                configHandler, _logger);
    }
}
