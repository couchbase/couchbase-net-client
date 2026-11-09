using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
using Couchbase.Utils;
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

        [Fact]
        public async Task ClusterMapChangeNotification_OnANodeNoBucketOwns_RefreshesTheClusterConfig()
        {
            // A brief push of version 1/1102.
            byte[] briefPush =
            {
                130, 1, 0, 0, 16, 0, 0, 0, 0, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 4, 78
            };
            using var context = CreateContextWithoutPolling();
            var fetched = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var globalNode = MockKvNode(owner: null, onFetch: () => fetched.TrySetResult(true));
            context.AddNode(globalNode.Object);
            context.GlobalConfigRefresher.Start();
            using var clusterNode = CreateClusterNode(context, new Mock<IConnectionPool>().Object);

            clusterNode.HandleClusterMapChangeNotification(
                new SlicedMemoryOwner<byte>(new FakeMemoryOwner<byte>(new Memory<byte>(briefPush))));

            // No timeout. A push that reaches no refresher hangs the test, which is easy to diagnose.
            await fetched.Task;
        }

        [Fact]
        public async Task NotMyVBucket_WithNoConfig_FetchesNothing()
        {
            // Like the Java SDK, the op retries and the config comes from a push or a poll.
            using var context = CreateContextWithoutPolling();
            using var bucket = new CouchbaseBucket("default", context, new Mock<IScopeFactory>().Object,
                new Mock<IRetryOrchestrator>().Object, new Mock<IVBucketKeyMapperFactory>().Object,
                new Mock<ILogger<CouchbaseBucket>>().Object, new Redactor(RedactionLevel.None),
                new Mock<IBootstrapperFactory>().Object, NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object, new BestEffortRetryStrategy(), new BucketConfig(),
                context.ServiceProvider.GetRequiredService<IConfigRefresherFactory>());
            var bucketNode = MockKvNode(owner: bucket, onFetch: () => { });
            bucket.Nodes.Add(bucketNode.Object);
            bucket.ConfigRefresher.Start();

            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.SendAsync(It.IsAny<IOperation>(), It.IsAny<CancellationToken>()))
                .Returns((IOperation op, CancellationToken _) =>
                {
                    op.HandleOperationCompleted(
                        AsyncState.BuildErrorResponse(op.Opaque, ResponseStatus.VBucketBelongsToAnotherServer));
                    return Task.CompletedTask;
                });
            using var clusterNode = CreateClusterNode(context, pool.Object);
            clusterNode.Owner = bucket;

            using var op = new Get<object> { Key = "key" };
            Assert.Equal(ResponseStatus.VBucketBelongsToAnotherServer, await clusterNode.SendAsync(op));

            pool.Verify(p => p.TrySendImmediatelyAsync(It.IsAny<IOperation>(), It.IsAny<CancellationToken>()), Times.Never);
            bucketNode.Verify(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetClusterMap_WithAFailedStatus_Throws()
        {
            // A null config means the server has nothing newer, so a failure must not return one.
            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.TrySendImmediatelyAsync(It.IsAny<IOperation>(), It.IsAny<CancellationToken>()))
                .Returns((IOperation op, CancellationToken _) =>
                {
                    op.HandleOperationCompleted(AsyncState.BuildErrorResponse(op.Opaque, ResponseStatus.TemporaryFailure));
                    return Task.FromResult(true);
                });
            using var context = new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"));
            using var clusterNode = CreateClusterNode(context, pool.Object);

            await Assert.ThrowsAsync<TemporaryFailureException>(() => clusterNode.GetClusterMap());
        }

        [Fact]
        public async Task GetClusterMap_WithATimeout_TimesOutAfterItAndNotAfterKvTimeout()
        {
            // The node never replies.
            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.TrySendImmediatelyAsync(It.IsAny<IOperation>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.KvTimeout = TimeSpan.FromHours(1);
            using var context = new ClusterContext(null, options);
            using var clusterNode = CreateClusterNode(context, pool.Object);

            // No deadline. A fetch that waits for KvTimeout hangs the test, which is easy to diagnose.
            await Assert.ThrowsAsync<UnambiguousTimeoutException>(() =>
                clusterNode.GetClusterMap(timeout: TimeSpan.FromMilliseconds(50)));
        }

        private static ClusterContext CreateContextWithoutPolling()
        {
            // With no polling, only the event under test makes a refresher fetch.
            var options = new ClusterOptions().WithPasswordAuthentication("username", "password");
            options.EnableConfigPolling = false;
            return new ClusterContext(null, options);
        }

        private static Mock<IClusterNode> MockKvNode(IBucket owner, Action onFetch)
        {
            var node = new Mock<IClusterNode>();
            node.SetupGet(x => x.EndPoint).Returns(new HostEndpointWithPort("node-0", 11210));
            node.SetupGet(x => x.HasKv).Returns(true);
            node.SetupGet(x => x.KeyEndPoints).Returns(new List<HostEndpointWithPort>());
            node.SetupProperty(x => x.Owner, owner);
            node.Setup(x => x.GetClusterMap(It.IsAny<ConfigVersion?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
                .Callback(onFetch)
                .ReturnsAsync((BucketConfig)null);
            return node;
        }

        private ClusterNode CreateClusterNode(ClusterContext context, IConnectionPool connectionPool)
        {
            var connectionPoolFactory = new Mock<IConnectionPoolFactory>();
            connectionPoolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>())).Returns(connectionPool);

            return new ClusterNode(
                context: context,
                connectionPoolFactory: connectionPoolFactory.Object,
                logger: new Logger<ClusterNode>(new TestOutputLoggerFactory(outputHelper)),
                operationBuilderPool: new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                circuitBreaker: new CircuitBreaker(TimeProvider.System, new CircuitBreakerConfiguration { Enabled = false }),
                saslMechanismFactory: new Mock<ISaslMechanismFactory>().Object,
                redactor: new Redactor(RedactionLevel.None),
                endPoint: new HostEndpointWithPort("localhost", 11210),
                nodeAdapter: new NodeAdapter { Hostname = "localhost" },
                tracer: NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object);
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
