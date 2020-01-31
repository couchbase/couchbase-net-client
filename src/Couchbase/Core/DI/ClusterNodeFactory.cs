using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IClusterNodeFactory"/>.
    /// </summary>
    internal class ClusterNodeFactory : IClusterNodeFactory
    {
        private readonly ClusterContext _clusterContext;
        private readonly IConnectionFactory _connectionFactory;
        private readonly ILogger<ClusterNode> _logger;

        public ClusterNodeFactory(ClusterContext clusterContext, IConnectionFactory connectionFactory, ILogger<ClusterNode> logger)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IClusterNode> CreateAndConnectAsync(IPEndPoint endPoint)
        {
            var connection = await _connectionFactory.CreateAndConnectAsync(endPoint);
            var serverFeatures = await connection.Hello().ConfigureAwait(false);
            var errorMap = await connection.GetErrorMap().ConfigureAwait(false);
            await connection.Authenticate(_clusterContext.ClusterOptions, null, _clusterContext.CancellationToken).ConfigureAwait(false);

            var clusterNode = new ClusterNode(_clusterContext, _connectionFactory, _logger)
            {
                EndPoint = endPoint,
                Connection = connection,
                ServerFeatures = serverFeatures,
                ErrorMap = errorMap
            };

            clusterNode.BuildServiceUris();

            return clusterNode;
        }
    }
}
