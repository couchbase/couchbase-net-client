using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Test.Common.Utils;
using Microsoft.Extensions.Logging;
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

            var node1 = new ClusterNode(new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")), pool, new CircuitBreaker(),
                new Mock<IOperationConfigurator>().Object)
            {
                Owner = beerSample.Object,
                EndPoint = new HostEndpointWithPort("127.0.0.1", 10210)
            };

            var travelSample = new Mock<IBucket>();
            travelSample
                .Setup(x => x.Name)
                .Returns("travel-sample");
            var node2 = new ClusterNode(new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")), pool, new CircuitBreaker(),
                new Mock<IOperationConfigurator>().Object)
            {
                Owner = travelSample.Object,
                EndPoint = new HostEndpointWithPort("127.0.0.1", 10210)
            };

            Assert.NotEqual(node1.GetHashCode(), node2.GetHashCode());
        }

        [Fact]
        public async Task External_Cancellation_Is_Not_TimeoutException()
        {
            using var clusterNode = MockClusterNode("default");
            var op = new Get<object>();
            var cancelled = new CancellationToken(canceled: true);
            var cancellationTokenPair = new CancellationTokenPair(new CancellationTokenPairSource(externalToken: cancelled));
            await Assert.ThrowsAsync<OperationCanceledException>(() => clusterNode.SendAsync(op, cancellationTokenPair));
        }

        [Fact]
        public async Task Internal_Cancellation_Is_TimeoutException()
        {
            using var clusterNode = MockClusterNode("default");
            var op = new Get<object>();
            var cts = new CancellationTokenPairSource();
#if NET8_0_OR_GREATER
            await cts.CancelAsync();
#else
            cts.Cancel();
#endif

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

            var nodes = new BucketNodeList
            {
                clusterNode3,
                clusterNode1,
                clusterNode2,
                clusterNode4
            };

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

        /// <summary>
        /// A failed HELO used to be swallowed: the status was discarded, GetValue() returned null,
        /// and the caller quietly assigned ServerFeatureSet.Empty - leaving a connection in the pool
        /// with no negotiated features while operations were still framed as though it had them,
        /// which is how a rejected HELO produces corrupted document keys (NCBC-4146, NCBC-4287).
        /// </summary>
        [Fact]
        public async Task Failed_Hello_Fails_The_Connection()
        {
            using var clusterNode = MockClusterNode("default");

            var connection = new Mock<IConnection>();
            connection.SetupGet(c => c.ConnectionId).Returns(1UL);
            connection.SetupGet(c => c.ServerFeatures).Returns(ServerFeatureSet.Empty);
            connection
                .Setup(c => c.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IOperation>(),
                    It.IsAny<CancellationToken>()))
                .Returns((ReadOnlyMemory<byte> _, IOperation op, CancellationToken _) =>
                {
                    //Anything but Success. Not TransportFailure, which already had its own
                    //ConnectException path, so this exercises the new check rather than the old one.
                    op.HandleOperationCompleted(
                        AsyncState.BuildErrorResponse(op.Opaque, ResponseStatus.InternalError));
                    return default;
                });

            var exception = await Assert.ThrowsAsync<ConnectException>(() =>
                ((IConnectionInitializer) clusterNode).InitializeConnectionAsync(connection.Object, default));

            Assert.Contains("HELO failed", exception.Message);
        }

        /// <summary>
        /// The error map is fetched after HELO, so it is still null when HELO itself fails. The
        /// "not found in Error Map" warning claims a lookup that never happened, which sends anyone
        /// reading the log after a failed HELO looking at the error map instead of the handshake.
        /// </summary>
        [Fact]
        public async Task Failed_Hello_Does_Not_Blame_The_Error_Map()
        {
            var logger = new Mock<ILogger<ClusterNode>>();
            logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

            using var clusterNode = MockClusterNode("default", logger: logger.Object);

            var connection = new Mock<IConnection>();
            connection.SetupGet(c => c.ConnectionId).Returns(1UL);
            connection.SetupGet(c => c.ServerFeatures).Returns(ServerFeatureSet.Empty);
            connection
                .Setup(c => c.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IOperation>(),
                    It.IsAny<CancellationToken>()))
                .Returns((ReadOnlyMemory<byte> _, IOperation op, CancellationToken _) =>
                {
                    op.HandleOperationCompleted(
                        AsyncState.BuildErrorResponse(op.Opaque, ResponseStatus.InternalError));
                    return default;
                });

            await Assert.ThrowsAsync<ConnectException>(() =>
                ((IConnectionInitializer) clusterNode).InitializeConnectionAsync(connection.Object, default));

            logger.Verify(
                l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);
        }

        private ClusterNode MockClusterNode(string bucketName, string hostname = "localhost",
            ILogger<ClusterNode> logger = null)
        {
            var pool = new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy());
            var loggerFactory = new TestOutputLoggerFactory(outputHelper);
            logger ??= new Logger<ClusterNode>(loggerFactory);
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
                context: new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password")),
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
