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
        private readonly TypedRedactor _redactor;
        private readonly IRequestTracer _tracer;

        public ClusterNodeFactory(ClusterContext clusterContext, IConnectionPoolFactory connectionPoolFactory, ILogger<ClusterNode> logger,
            ObjectPool<OperationBuilder> operationBuilderPool, ICircuitBreaker circuitBreaker, ISaslMechanismFactory saslMechanismFactory,
            TypedRedactor redactor, IRequestTracer tracer)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _connectionPoolFactory = connectionPoolFactory ?? throw new ArgumentNullException(nameof(connectionPoolFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationBuilderPool = operationBuilderPool ?? throw new ArgumentNullException(nameof(operationBuilderPool));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentException(nameof(circuitBreaker));
            _saslMechanismFactory = saslMechanismFactory ?? throw new ArgumentNullException(nameof(saslMechanismFactory));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        /// <inheritdoc />
        public Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, CancellationToken cancellationToken = default)
        {
            return CreateAndConnectAsync(endPoint, null, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter, CancellationToken cancellationToken = default)
        {
            var clusterNode = new ClusterNode(_clusterContext, _connectionPoolFactory, _logger,
                _operationBuilderPool, _circuitBreaker, _saslMechanismFactory, _redactor, endPoint,
                nodeAdapter, _tracer);

            //ensure server calls are made to set the state
            await clusterNode.InitializeAsync().ConfigureAwait(false);

            return clusterNode;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
