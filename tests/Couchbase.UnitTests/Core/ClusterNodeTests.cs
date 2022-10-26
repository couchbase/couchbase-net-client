using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
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

            var node1 = new ClusterNode(new ClusterContext(), pool, new CircuitBreaker())
            {
                Owner = new Mock<IBucket>().Object,
                //EndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10210)
            };

            var node2 = new ClusterNode(new ClusterContext(), pool, new CircuitBreaker())
            {
                Owner = new Mock<IBucket>().Object,
                //EndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10210)
            };

            Assert.NotEqual(node1.GetHashCode(), node2.GetHashCode());
        }

        [Fact]
        public async void External_Cancellation_Is_Not_TimeoutException()
        {
            var clusterNode = MockClusterNode();
            var op = new Get<object>();
            var cancelled = new CancellationToken(canceled: true);
            var cancellationTokenPair = new CancellationTokenPair(new CancellationTokenPairSource(externalToken: cancelled, internalToken: CancellationToken.None));
            await Assert.ThrowsAsync<OperationCanceledException>(() => clusterNode.SendAsync(op, cancellationTokenPair));

            // the Elapsed stopwatch should stop after the op is timed out.
            var elapsedAfterThrow = op.Elapsed.Ticks;
            await System.Threading.Tasks.Task.Delay(50);
            Assert.Equal(elapsedAfterThrow, op.Elapsed.Ticks);
        }

        [Fact]
        public async void Internal_Cancellation_Is_TimeoutException()
        {
            var clusterNode = MockClusterNode();
            var op = new Get<object>();
            var cancelled = new CancellationToken(canceled: true);
            var cancellationTokenPair = new CancellationTokenPair(new CancellationTokenPairSource(externalToken: CancellationToken.None, internalToken: cancelled));
            await Assert.ThrowsAsync<UnambiguousTimeoutException>(() => clusterNode.SendAsync(op, cancellationTokenPair));

            // the Elapsed stopwatch should stop after the op is timed out.
            var elapsedAfterThrow = op.Elapsed.Ticks;
            await System.Threading.Tasks.Task.Delay(50);
            Assert.Equal(elapsedAfterThrow, op.Elapsed.Ticks);
        }

        private ClusterNode MockClusterNode()
        {
            var pool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            var loggerFactory = new TestOutputLoggerFactory(outputHelper);
            var logger = new Logger<ClusterNode>(loggerFactory);
            var mockConnectionPool = new Mock<IConnectionPool>();

            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(mockConnectionPool.Object);

            var node1 = new ClusterNode(
                context: new ClusterContext(),
                connectionPoolFactory: mockConnectionPoolFactory.Object,
                logger: logger,
                operationBuilderPool: pool,
                circuitBreaker: new CircuitBreaker(new CircuitBreakerConfiguration() { Enabled = true }),
                saslMechanismFactory: new Mock<Couchbase.Core.DI.ISaslMechanismFactory>().Object,
                redactor: new(Couchbase.Core.Logging.RedactionLevel.None),
                endPoint: new("localhost", 8091),
                nodeAdapter: new() { Hostname = "localhost" },
                tracer: new Couchbase.Core.Diagnostics.Tracing.NoopRequestTracer()
                )
            {
                Owner = new Mock<IBucket>().Object,
            };

            return node1;
        }
    }
}
