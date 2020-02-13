using System;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolScaleControllerFactory"/>.
    /// </summary>
    internal class ConnectionPoolScaleControllerFactory : IConnectionPoolScaleControllerFactory
    {
        private readonly ClusterOptions _clusterOptions;
        private readonly IRedactor _redactor;
        private readonly ILogger<DefaultConnectionPoolScaleController> _logger;

        public ConnectionPoolScaleControllerFactory(ClusterOptions clusterOptions, IRedactor redactor,
            ILogger<DefaultConnectionPoolScaleController> logger)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IConnectionPoolScaleController Create()
        {
            return new DefaultConnectionPoolScaleController(_redactor, _logger)
            {
                IdleConnectionTimeout = _clusterOptions.IdleKvConnectionTimeout
            };
        }
    }
}
