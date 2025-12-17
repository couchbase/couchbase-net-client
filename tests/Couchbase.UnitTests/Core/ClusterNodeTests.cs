using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core
{
    public class ClusterNodeTests
    {
        private readonly ITestOutputHelper outputHelper;

        public ClusterNodeTests(ITestOutputHelper outputHelper)
        {
            this.outputHelper = outputHelper;
        }

        [Fact]
        public void Test_GetHashCode()
        {
            var pool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());

            var beerSample = new Mock<IBucket>();
                beerSample
                    .Setup(x => x.Name)
                    .Returns("beer-sample");

            var node1 = new ClusterNode(new ClusterContext(), pool, new CircuitBreaker(),
                new Mock<IOperationConfigurator>().Object)
            {
                Owner = beerSample.Object,
                EndPoint = new HostEndpointWithPort("127.0.0.1", 10210)
            };

            var travelSample = new Mock<IBucket>();
            travelSample
                .Setup(x => x.Name)
                .Returns("travel-sample");
            var node2 = new ClusterNode(new ClusterContext(), pool, new CircuitBreaker(),
                new Mock<IOperationConfigurator>().Object)
            {
                Owner = travelSample.Object,
                EndPoint = new HostEndpointWithPort("127.0.0.1", 10210)
            };

            Assert.NotEqual(node1.GetHashCode(), node2.GetHashCode());
        }

        [Fact]
        public async void External_Cancellation_Is_Not_TimeoutException()
        {
            using var clusterNode = MockClusterNode("default");
            var op = new Get<object>();
            var cancelled = new CancellationToken(canceled: true);
            var cancellationTokenPair = new CancellationTokenPair(new CancellationTokenPairSource(externalToken: cancelled));
            await Assert.ThrowsAsync<OperationCanceledException>(() => clusterNode.SendAsync(op, cancellationTokenPair));
        }

        [Fact]
        public async void Internal_Cancellation_Is_TimeoutException()
        {
            using var clusterNode = MockClusterNode("default");
            var op = new Get<object>();
            var cts = new CancellationTokenPairSource();
            cts.Cancel();
            var cancellationTokenPair = new CancellationTokenPair(cts);
            await Assert.ThrowsAsync<UnambiguousTimeoutException>(() => clusterNode.SendAsync(op, cancellationTokenPair));
        }

        [Fact]
        public void Test_ClusterNodeCollection()
        {
            using var clusterNode1 = MockClusterNode("default1", "localhost1");
            using var clusterNode2 = MockClusterNode("default2", "localhost2");
            using var clusterNode3 = MockClusterNode("default1", "localhost1");
            using var clusterNode4 = MockClusterNode("default2", "localhost2");

            var nodes = new ClusterNodeCollection();
            nodes.Add(clusterNode3);
            nodes.Add(clusterNode1);
            nodes.Add(clusterNode2);
            nodes.Add(clusterNode4);

            nodes.Remove(clusterNode1.EndPoint, "default1", out var node1);
            nodes.Remove(clusterNode2.EndPoint, "default2", out var node2);
            nodes.Remove(clusterNode3.EndPoint, "default1", out var node3);
            nodes.Remove(clusterNode4.EndPoint, "default2", out var node4);

            nodes.Add(clusterNode3);
            nodes.Add(clusterNode1);
            nodes.Add(clusterNode2);
            nodes.Add(clusterNode4);

            nodes.Remove(clusterNode1.EndPoint, "default1", out node1);
            nodes.Remove(clusterNode2.EndPoint, "default2", out node2);
            nodes.Remove(clusterNode3.EndPoint, "default1", out node3);
            nodes.Remove(clusterNode4.EndPoint, "default2", out node4);
        }

        private ClusterNode MockClusterNode(string bucketName, string hostname = "localhost")
        {
            var pool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            var loggerFactory = new TestOutputLoggerFactory(outputHelper);
            var logger = new Logger<ClusterNode>(loggerFactory);
            var mockConnectionPool = new Mock<IConnectionPool>();
            var owner = new Mock<IBucket>();
            owner.
                Setup(x => x.Name).
                Returns(bucketName);

            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(mockConnectionPool.Object);

            var node1 = new ClusterNode(
                context: new ClusterContext(),
                connectionPoolFactory: mockConnectionPoolFactory.Object,
                logger: logger,
                operationBuilderPool: pool,
                circuitBreaker: new CircuitBreaker(TimeProvider.System, new CircuitBreakerConfiguration { Enabled = true }),
                saslMechanismFactory: new Mock<Couchbase.Core.DI.ISaslMechanismFactory>().Object,
                redactor: new(Couchbase.Core.Logging.RedactionLevel.None),
                endPoint: new(hostname, 8091),
                nodeAdapter: new() { Hostname = hostname},
                tracer: new Couchbase.Core.Diagnostics.Tracing.NoopRequestTracer(),
                new Mock<IOperationConfigurator>().Object)
            {
                Owner = owner.Object,
            };

            return node1;
        }
    }
}
