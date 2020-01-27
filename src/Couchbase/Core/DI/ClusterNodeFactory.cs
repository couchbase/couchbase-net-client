using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.IO.Transcoders;
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
        private readonly ITypeTranscoder _transcoder;
        private readonly ICircuitBreaker _circuitBreaker;

        public ClusterNodeFactory(ClusterContext clusterContext, IConnectionFactory connectionFactory, ILogger<ClusterNode> logger, ITypeTranscoder transcoder, ICircuitBreaker circuitBreaker)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
        }

        /// <inheritdoc />
        public async Task<IClusterNode> CreateAndConnectAsync(IPEndPoint endPoint)
        {
            var connection = await _connectionFactory.CreateAndConnectAsync(endPoint);
            var serverFeatures = await connection.Hello(_transcoder).ConfigureAwait(false);
            var errorMap = await connection.GetErrorMap(_transcoder).ConfigureAwait(false);
            await connection.Authenticate(_clusterContext.ClusterOptions, null, _clusterContext.CancellationToken).ConfigureAwait(false);

            var clusterNode = new ClusterNode(_clusterContext, _connectionFactory, _logger, _transcoder, _circuitBreaker)
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
