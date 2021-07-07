using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

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
        private readonly ObjectPool<OperationBuilder> _operationBuilderPool;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly ISaslMechanismFactory _saslMechanismFactory;
        private readonly IIpEndPointService _ipEndPointService;
        private readonly IRedactor _redactor;
        private readonly IRequestTracer _tracer;
        private readonly IMeter _meter;

        public ClusterNodeFactory(ClusterContext clusterContext, IConnectionPoolFactory connectionPoolFactory, ILogger<ClusterNode> logger,
            ObjectPool<OperationBuilder> operationBuilderPool, ICircuitBreaker circuitBreaker, ISaslMechanismFactory saslMechanismFactory,
            IIpEndPointService ipEndPointService, IRedactor redactor, IRequestTracer tracer, IMeter meter)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _connectionPoolFactory = connectionPoolFactory ?? throw new ArgumentNullException(nameof(connectionPoolFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationBuilderPool = operationBuilderPool ?? throw new ArgumentNullException(nameof(operationBuilderPool));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
            _saslMechanismFactory = saslMechanismFactory ?? throw new ArgumentNullException(nameof(saslMechanismFactory));
            _ipEndPointService = ipEndPointService ?? throw new ArgumentNullException(nameof(ipEndPointService));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _meter = meter ?? throw new ArgumentNullException(nameof(meter));
        }

        /// <inheritdoc />
        public Task<IClusterNode> CreateAndConnectAsync(HostEndpoint endPoint, BucketType bucketType, CancellationToken cancellationToken = default)
        {
            return CreateAndConnectAsync(endPoint, bucketType, null, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IClusterNode> CreateAndConnectAsync(HostEndpoint endPoint, BucketType bucketType, NodeAdapter? nodeAdapter, CancellationToken cancellationToken = default)
        {
            var ipEndPoint = await _ipEndPointService.GetIpEndPointAsync(endPoint.Host, endPoint.Port.GetValueOrDefault(), cancellationToken).ConfigureAwait(false);

            //for recording k/v latencies per request
            var valueRecorder = _meter.ValueRecorder(OuterRequestSpans.ServiceSpan.Kv.Name);

            var clusterNode = new ClusterNode(_clusterContext, _connectionPoolFactory, _logger,
                _operationBuilderPool, _circuitBreaker, _saslMechanismFactory, _redactor, ipEndPoint, bucketType,
                nodeAdapter, _tracer, valueRecorder)
            {
                BootstrapEndpoint = endPoint
            };

            //ensure server calls are made to set the state
            await clusterNode.InitializeAsync().ConfigureAwait(false);

            return clusterNode;
        }
    }
}
