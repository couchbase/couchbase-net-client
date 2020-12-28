using System;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections.Channels
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolFactory"/>.
    /// </summary>
    internal class ChannelConnectionPoolFactory : IConnectionPoolFactory
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly ClusterOptions _clusterOptions;
        private readonly IConnectionPoolScaleControllerFactory _scaleControllerFactory;
        private readonly IRedactor _redactor;
        private readonly ILogger<ChannelConnectionPool> _dataFlowLogger;

        public ChannelConnectionPoolFactory(IConnectionFactory connectionFactory, ClusterOptions clusterOptions,
            IConnectionPoolScaleControllerFactory scaleControllerFactory, IRedactor redactor,
            ILogger<ChannelConnectionPool> dataFlowLogger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _scaleControllerFactory = scaleControllerFactory ?? throw new ArgumentNullException(nameof(scaleControllerFactory));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _dataFlowLogger = dataFlowLogger ?? throw new ArgumentNullException(nameof(dataFlowLogger));
        }

        /// <inheritdoc />
        public IConnectionPool Create(ClusterNode clusterNode)
        {
            if (_clusterOptions.NumKvConnections <= 1 && _clusterOptions.MaxKvConnections <= 1)
            {
                return new SingleConnectionPool(clusterNode, _connectionFactory);
            }
            else
            {
                var scaleController = _scaleControllerFactory.Create();

                return new ChannelConnectionPool(clusterNode, _connectionFactory, scaleController,
                    _redactor, _dataFlowLogger, (int) _clusterOptions.KvSendQueueCapacity)
                {
                    MinimumSize = _clusterOptions.NumKvConnections,
                    MaximumSize = _clusterOptions.MaxKvConnections
                };
            }
        }
    }
}
