using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.IO.Connections;
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
        private readonly IConnectionPoolFactory _connectionPoolFactory;
        private readonly ILogger<ClusterNode> _logger;
        private readonly ITypeTranscoder _transcoder;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly ISaslMechanismFactory _saslMechanismFactory;
        private readonly IIpEndPointService _ipEndPointService;

        public ClusterNodeFactory(ClusterContext clusterContext, IConnectionPoolFactory connectionPoolFactory, ILogger<ClusterNode> logger,
            ITypeTranscoder transcoder, ICircuitBreaker circuitBreaker, ISaslMechanismFactory saslMechanismFactory,
            IIpEndPointService ipEndPointService)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _connectionPoolFactory = connectionPoolFactory ?? throw new ArgumentNullException(nameof(connectionPoolFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
            _saslMechanismFactory = saslMechanismFactory;
            _ipEndPointService = ipEndPointService ?? throw new ArgumentNullException(nameof(ipEndPointService));
        }

        /// <inheritdoc />
        public async Task<IClusterNode> CreateAndConnectAsync(HostEndpoint endPoint, CancellationToken cancellationToken = default)
        {
            var ipEndPoint = await _ipEndPointService.GetIpEndPointAsync(endPoint.Host, endPoint.Port.GetValueOrDefault(), cancellationToken);

            var clusterNode = new ClusterNode(_clusterContext, _connectionPoolFactory, _logger,
                _transcoder, _circuitBreaker, _saslMechanismFactory, ipEndPoint)
            {
                BootstrapEndpoint = endPoint
            };

            //ensure server calls are made to set the state
            await clusterNode.InitializeAsync().ConfigureAwait(false);

            return clusterNode;
        }
    }
}
