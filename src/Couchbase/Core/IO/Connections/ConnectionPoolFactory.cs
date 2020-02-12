using System;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections.DataFlow;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolFactory"/>.
    /// </summary>
    internal class ConnectionPoolFactory : IConnectionPoolFactory
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly ClusterOptions _clusterOptions;
        private readonly ILogger<DataFlowConnectionPool> _dataFlowLogger;

        public ConnectionPoolFactory(IConnectionFactory connectionFactory, ClusterOptions clusterOptions,
            ILogger<DataFlowConnectionPool> dataFlowLogger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
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
                return new DataFlowConnectionPool(clusterNode, _connectionFactory, _dataFlowLogger)
                {
                    MinimumSize = _clusterOptions.NumKvConnections,
                    MaximumSize = _clusterOptions.MaxKvConnections
                };
            }
        }
    }
}
