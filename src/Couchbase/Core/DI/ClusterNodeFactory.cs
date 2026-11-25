using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using BucketNotFoundException = Couchbase.Management.Buckets.BucketNotFoundException;

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
        private readonly ISaslMechanismFactory _saslMechanismFactory;
        private readonly Redactor _redactor;
        private readonly IRequestTracer _tracer;
        private readonly IOperationConfigurator _operationConfigurator;

        public ClusterNodeFactory(ClusterContext clusterContext, IConnectionPoolFactory connectionPoolFactory, ILogger<ClusterNode> logger,
            ObjectPool<OperationBuilder> operationBuilderPool, ISaslMechanismFactory saslMechanismFactory,
            Redactor redactor, IRequestTracer tracer, IOperationConfigurator operationConfigurator)
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _connectionPoolFactory = connectionPoolFactory ?? throw new ArgumentNullException(nameof(connectionPoolFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _operationBuilderPool = operationBuilderPool ?? throw new ArgumentNullException(nameof(operationBuilderPool));
            _saslMechanismFactory = saslMechanismFactory ?? throw new ArgumentNullException(nameof(saslMechanismFactory));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _operationConfigurator = operationConfigurator;
        }

        /// <inheritdoc />
        public Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, CancellationToken cancellationToken = default)
        {
            return CreateAndConnectAsync(endPoint, null, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter, CancellationToken cancellationToken = default)
        {
            return CreateAndConnectCoreAsync(endPoint, nodeAdapter, null, acceptRefusedBucket: false);
        }

        /// <inheritdoc />
        public Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter, string bucketName,
            CancellationToken cancellationToken = default)
        {
            return CreateAndConnectAsync(endPoint, nodeAdapter, bucketName, acceptRefusedBucket: false, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter, string bucketName,
            bool acceptRefusedBucket, CancellationToken cancellationToken = default)
        {
            if (bucketName == null)
            {
                throw new ArgumentNullException(nameof(bucketName));
            }

            return CreateAndConnectCoreAsync(endPoint, nodeAdapter, bucketName, acceptRefusedBucket);
        }

        /// <inheritdoc />
        public IClusterNode CreateClusterNode(HostEndpointWithPort endPoint, NodeAdapter nodeAdapter)
        {
            var clusterNode = CreateNode(endPoint, nodeAdapter, bucketName: null);
            clusterNode.StartConnecting();
            return clusterNode;
        }

        private ClusterNode CreateNode(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter, string? bucketName)
        {
            // Resolved per node rather than held as a field, so each node gets its own breaker.
            var circuitBreaker = _clusterContext.ServiceProvider.GetRequiredService<ICircuitBreaker>();

            return new ClusterNode(_clusterContext, _connectionPoolFactory, _logger,
                _operationBuilderPool, circuitBreaker, _saslMechanismFactory, _redactor, endPoint,
                nodeAdapter, _tracer, _operationConfigurator, bucketName);
        }

        private async Task<IClusterNode> CreateAndConnectCoreAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter,
            string? bucketName, bool acceptRefusedBucket)
        {
            var clusterNode = CreateNode(endPoint, nodeAdapter, bucketName);

            //ensure server calls are made to set the state
            try
            {
                await clusterNode.InitializeAsync().ConfigureAwait(false);
            }
            catch (BucketNotFoundException) when (acceptRefusedBucket)
            {
                // The node joins with no connection and its pool keeps trying until the node accepts the bucket.
            }
            catch
            {
                // Closes the connections and stops the refill loop of a node that nobody will own.
                clusterNode.Dispose();
                throw;
            }

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
