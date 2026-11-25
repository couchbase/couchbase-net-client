using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Connections.DataFlow;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Helpers;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core
{
    /// <summary>
    /// A cluster node, a node with no bucket, joins at once and connects in the background with a pool of one
    /// connection (NCBC-4423).
    /// </summary>
    public class ClusterNodeBackgroundConnectTests
    {
        // Bounds each wait, so a regression fails instead of hanging.
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        private static readonly HostEndpointWithPort EndPoint = new("localhost", 11210);

        #region Pool of a cluster node

        // RFC 48 keeps one KV connection with no bucket per node. SingleConnectionPool does not retry a failed first
        // connect, so a cluster node never gets it, also when the configured size is 1.
        [Theory]
        [InlineData(true, 1, 1)]
        [InlineData(true, 2, 5)]
        [InlineData(false, 1, 1)]
        [InlineData(false, 2, 5)]
        public void PoolFactory_ClusterNode_GetsARetryingPoolOfOneConnection(bool channelPools, int numKv, int maxKv)
        {
            using var context = CreateContext(options =>
            {
                options.NumKvConnections = numKv;
                options.MaxKvConnections = maxKv;
            });

            using var node = CreateNode(context, PoolFactory(context, channelPools), bucketName: null);

            var pool = node.ConnectionPool;
            Assert.IsType(channelPools ? typeof(ChannelConnectionPool) : typeof(DataFlowConnectionPool), pool);
            Assert.Equal((1, 1), (pool.MinimumSize, pool.MaximumSize));
        }

        [Theory]
        [InlineData(true, 1, 1, typeof(SingleConnectionPool))]
        [InlineData(true, 2, 5, typeof(ChannelConnectionPool))]
        [InlineData(false, 1, 1, typeof(SingleConnectionPool))]
        [InlineData(false, 2, 5, typeof(DataFlowConnectionPool))]
        public void PoolFactory_BucketNode_KeepsTheConfiguredPool(bool channelPools, int numKv, int maxKv, Type poolType)
        {
            using var context = CreateContext(options =>
            {
                options.NumKvConnections = numKv;
                options.MaxKvConnections = maxKv;
            });

            using var node = CreateNode(context, PoolFactory(context, channelPools), bucketName: "default");

            var pool = node.ConnectionPool;
            Assert.IsType(poolType, pool);
            if (pool is not SingleConnectionPool)
            {
                Assert.Equal((numKv, maxKv), (pool.MinimumSize, pool.MaximumSize));
            }
        }

        #endregion

        #region Background start

        // The node returns before its connect ends and stays whatever the result. The error is logged, not thrown into
        // an unobserved task, and the pool keeps trying on the SDK clock until the node is disposed.
        [Fact]
        public async Task CreateClusterNode_FirstConnectFails_KeepsTheNodeAndItsPoolRetriesUntilDisposed()
        {
            var clock = new SteppableClock();
            using var context = CreateContext();
            var logger = new CapturingLogger();
            var connections = new ConnectionFactory { Error = new SocketException((int)SocketError.ConnectionRefused) };
            var factory = CreateFactory(context, new ChannelConnectionPoolFactory(connections, context.ClusterOptions,
                ScaleControllers(), new Redactor(RedactionLevel.None), NullLogger<ChannelConnectionPool>.Instance, clock),
                logger);

            var node = factory.CreateClusterNode(EndPoint, KvAdapter());

            await logger.WaitForAsync(LogLevel.Warning).WaitAsync(Deadline);
            Assert.False(node.IsDead);
            Assert.True(node.IsAwaitingFirstConnection);
            var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
            Assert.IsType<SocketException>(warning.Exception);

            await clock.WaitForSleepAsync(ChannelConnectionPool.MinRefillDelay).WaitAsync(Deadline);
            clock.Advance(ChannelConnectionPool.MinRefillDelay);
            await connections.WaitForAttemptsAsync(2).WaitAsync(Deadline);
            await clock.WaitForSleepAsync(TimeSpan.FromTicks(ChannelConnectionPool.MinRefillDelay.Ticks * 2)).WaitAsync(Deadline);

            node.Dispose();
            clock.Advance(ChannelConnectionPool.MaxRefillDelay);
            clock.Advance(ChannelConnectionPool.MaxRefillDelay);

            Assert.Equal(2, connections.Attempts);
            Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        }

        // A dispose during the first attempt cancels it. The stopped attempt is not logged as a failure.
        [Fact]
        public async Task CreateClusterNode_DisposedDuringTheFirstConnect_StopsItAndLogsNoFailure()
        {
            using var context = CreateContext();
            var logger = new CapturingLogger();
            var connections = new ConnectionFactory { Hang = true };
            var factory = CreateFactory(context, PoolFactory(context, channelPools: true, connections), logger);

            var node = factory.CreateClusterNode(EndPoint, KvAdapter());
            await connections.WaitForAttemptsAsync(1).WaitAsync(Deadline);
            node.Dispose();

            await connections.WaitForCancelledAsync(1).WaitAsync(Deadline);
            await logger.WaitForAsync(LogLevel.Debug, "stopped").WaitAsync(Deadline);
            Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
            Assert.Equal(1, connections.Attempts);
        }

        [Fact]
        public void CreateClusterNode_WithNoKv_StartsNoConnect()
        {
            using var context = CreateContext();
            var pool = new Mock<IConnectionPool>();
            var poolFactory = new Mock<IConnectionPoolFactory>();
            poolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>())).Returns(pool.Object);
            var factory = CreateFactory(context, poolFactory.Object, new CapturingLogger());

            using var node = factory.CreateClusterNode(new HostEndpointWithPort("query", 0),
                Adapter("query", new Services { N1Ql = 8093 }));

            Assert.True(node.HasQuery);
            pool.Verify(p => p.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // The connect outlives the caller, so it must not keep the caller's trace or other async locals alive.
        [Fact]
        public async Task CreateClusterNode_FirstConnect_DoesNotCarryTheCallersExecutionContext()
        {
            using var context = CreateContext();
            var callerValue = new AsyncLocal<string>();
            var seen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pool = new Mock<IConnectionPool>();
            pool.Setup(p => p.InitializeAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.Run(() => seen.TrySetResult(callerValue.Value)));
            var poolFactory = new Mock<IConnectionPoolFactory>();
            poolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>())).Returns(pool.Object);
            var factory = CreateFactory(context, poolFactory.Object, new CapturingLogger());

            callerValue.Value = "caller";
            using var node = factory.CreateClusterNode(EndPoint, KvAdapter());

            Assert.Null(await seen.Task.WaitAsync(Deadline));
            Assert.Equal("caller", callerValue.Value);
        }

        #endregion

        #region Late HELLO

        // A cluster node joins before its HELLO, so the cluster feature flags must follow the HELLO that lands later.
        [Fact]
        public async Task Hello_OfANodeAlreadyInTheContext_RecomputesTheClusterFeatures()
        {
            using var context = CreateContext();
            var poolFactory = new Mock<IConnectionPoolFactory>();
            poolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>())).Returns(new Mock<IConnectionPool>().Object);
            using var node = CreateNode(context, poolFactory.Object, bucketName: null);
            context.AddNode(node);
            Assert.False(context.SupportsPreserveTtl);

            var connection = new Mock<IConnection>();
            connection.SetupProperty(c => c.ServerFeatures, ServerFeatureSet.Empty);
            connection
                .Setup(c => c.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IOperation>(), It.IsAny<CancellationToken>()))
                .Returns((ReadOnlyMemory<byte> _, IOperation op, CancellationToken _) =>
                {
                    // HELLO succeeds. The setup that follows fails, which does not undo the features.
                    op.HandleOperationCompleted(op.OpCode == OpCode.Helo
                        ? HelloResponse(op.Opaque, ServerFeatures.PreserveTtl, ServerFeatures.SubdocBinaryXattr,
                            ServerFeatures.XError, ServerFeatures.Collections)
                        : AsyncState.BuildErrorResponse(op.Opaque, ResponseStatus.InternalError));
                    return default;
                });

            await Assert.ThrowsAnyAsync<Exception>(() =>
                ((IConnectionInitializer)node).InitializeConnectionAsync(connection.Object, default).WaitAsync(Deadline));

            Assert.True(context.SupportsPreserveTtl);
            Assert.True(context.SupportsBinaryXattr);
        }

        private static SlicedMemoryOwner<byte> HelloResponse(uint opaque, params ServerFeatures[] features)
        {
            var packet = new byte[OperationHeader.Length + 2 * features.Length];
            packet[0] = (byte)Magic.ClientResponse;
            packet[1] = (byte)OpCode.Helo;
            BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(8), 2 * features.Length);
            BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(12), opaque);
            for (var i = 0; i < features.Length; i++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(OperationHeader.Length + 2 * i), (ushort)features[i]);
            }

            return new SlicedMemoryOwner<byte>(new FakeMemoryOwner<byte>(new Memory<byte>(packet)));
        }

        #endregion

        #region JWT re-authentication

        // Re-authentication refills the pool to its minimum under the pool lock. During the first connect of a cluster
        // node it must wait for that connect and leave the pool at one connection.
        [Fact]
        public async Task Reauthenticate_DuringTheFirstConnect_LeavesTheClusterPoolAtOneConnection()
        {
            using var context = CreateContext(options => options.Authenticator = new JwtAuthenticator("token"));
            var connections = new ConnectionFactory { Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
            var initializer = new Mock<IConnectionInitializer>();
            initializer.SetupGet(i => i.EndPoint).Returns(EndPoint);
            var pool = new ChannelConnectionPool(initializer.Object, connections, new Mock<IConnectionPoolScaleController>().Object,
                new Redactor(RedactionLevel.None), NullLogger<ChannelConnectionPool>.Instance, new SteppableClock(), 1024);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;
            var poolFactory = new Mock<IConnectionPoolFactory>();
            poolFactory.Setup(m => m.Create(It.IsAny<ClusterNode>())).Returns(pool);
            var factory = CreateFactory(context, poolFactory.Object, new CapturingLogger());

            using var node = factory.CreateClusterNode(EndPoint, KvAdapter());
            await connections.WaitForAttemptsAsync(1).WaitAsync(Deadline);
            var reauth = node.ReauthenticateKvConnectionsAsync();
            connections.Gate.SetResult(true);
            await reauth.WaitAsync(Deadline);

            Assert.False(node.IsAwaitingFirstConnection);
            Assert.Equal(1, pool.Size);
            Assert.Equal(1, connections.Attempts);
        }

        #endregion

        #region Helpers

        private static NodeAdapter KvAdapter() => Adapter("localhost", new Services { Kv = 11210 });

        private static NodeAdapter Adapter(string host, Services services)
        {
            var config = new BucketConfig
            {
                Name = BucketConfig.GlobalBucketName,
                NodesExt = [new NodesExt { Hostname = host, Services = services }]
            };
            config.OnDeserialized();
            return config.GetClusterNodes()[0];
        }

        private static ClusterContext CreateContext(Action<ClusterOptions> configure = null)
        {
            var options = new ClusterOptions().WithConnectionString("couchbases://localhost")
                .WithPasswordAuthentication("username", "password");
            configure?.Invoke(options);
            return new ClusterContext(new CancellationTokenSource(), options);
        }

        private static IConnectionPoolScaleControllerFactory ScaleControllers()
        {
            var factory = new Mock<IConnectionPoolScaleControllerFactory>();
            factory.Setup(f => f.Create()).Returns(() => new Mock<IConnectionPoolScaleController>().Object);
            return factory.Object;
        }

        private static IConnectionPoolFactory PoolFactory(ClusterContext context, bool channelPools,
            IConnectionFactory connections = null)
        {
            connections ??= new ConnectionFactory();
            return channelPools
                ? new ChannelConnectionPoolFactory(connections, context.ClusterOptions, ScaleControllers(),
                    new Redactor(RedactionLevel.None), NullLogger<ChannelConnectionPool>.Instance, new SteppableClock())
                : new ConnectionPoolFactory(connections, context.ClusterOptions, ScaleControllers(),
                    new Redactor(RedactionLevel.None), NullLogger<DataFlowConnectionPool>.Instance);
        }

        private static ClusterNode CreateNode(ClusterContext context, IConnectionPoolFactory poolFactory, string bucketName) =>
            new(context, poolFactory, NullLogger<ClusterNode>.Instance,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new CircuitBreaker(TimeProvider.System, new CircuitBreakerConfiguration { Enabled = false }),
                new Mock<ISaslMechanismFactory>().Object, new Redactor(RedactionLevel.None), EndPoint, KvAdapter(),
                NoopRequestTracer.Instance, new Mock<IOperationConfigurator>().Object, bucketName);

        private static ClusterNodeFactory CreateFactory(ClusterContext context, IConnectionPoolFactory poolFactory,
            ILogger<ClusterNode> logger) =>
            new(context, poolFactory, logger, new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ISaslMechanismFactory>().Object, new Redactor(RedactionLevel.None), NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object);

        /// <summary>
        /// Opens mock connections. It can fail each attempt, hang it until it is cancelled, or hold it until
        /// <see cref="Gate"/> completes.
        /// </summary>
        private sealed class ConnectionFactory : IConnectionFactory
        {
            private int _attempts;
            private int _cancelled;
            private readonly ConcurrentQueue<(Func<bool> Done, TaskCompletionSource<bool> Signal)> _waiters = new();

            public Exception Error { get; init; }

            public bool Hang { get; init; }

            public TaskCompletionSource<bool> Gate { get; init; }

            public int Attempts => Volatile.Read(ref _attempts);

            public async Task<IConnection> CreateAndConnectAsync(HostEndpointWithPort hostEndpoint,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _attempts);
                Signal();
                if (Error is not null)
                {
                    throw Error;
                }

                try
                {
                    if (Hang)
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    }

                    if (Gate is not null)
                    {
                        await Gate.Task.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref _cancelled);
                    Signal();
                    throw;
                }

                var connection = new Mock<IConnection>();
                connection.SetupGet(c => c.ConnectionId).Returns((ulong)Attempts);
                connection.SetupGet(c => c.EndPoint).Returns(new IPEndPoint(IPAddress.Loopback, 11210));
                connection.Setup(c => c.CloseAsync(It.IsAny<TimeSpan>())).Returns(default(ValueTask));
                return connection.Object;
            }

            public Task WaitForAttemptsAsync(int count) => WaitFor(() => Attempts >= count);

            public Task WaitForCancelledAsync(int count) => WaitFor(() => Volatile.Read(ref _cancelled) >= count);

            private Task WaitFor(Func<bool> done)
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Enqueue((done, signal));
                Signal();
                return signal.Task;
            }

            private void Signal()
            {
                foreach (var (done, signal) in _waiters)
                {
                    if (done())
                    {
                        signal.TrySetResult(true);
                    }
                }
            }
        }

        private sealed class CapturingLogger : ILogger<ClusterNode>
        {
            private readonly ConcurrentQueue<(LogLevel Level, string Message, TaskCompletionSource<bool> Signal)> _waiters = new();

            public ConcurrentQueue<(LogLevel Level, string Message, Exception Exception)> Entries { get; } = new();

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                var message = formatter(state, exception);
                Entries.Enqueue((logLevel, message, exception));
                foreach (var waiter in _waiters)
                {
                    if (waiter.Level == logLevel && message.Contains(waiter.Message))
                    {
                        waiter.Signal.TrySetResult(true);
                    }
                }
            }

            public Task WaitForAsync(LogLevel level, string text = "")
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Enqueue((level, text, signal));
                if (Entries.Any(e => e.Level == level && e.Message.Contains(text)))
                {
                    signal.TrySetResult(true);
                }

                return signal.Task;
            }
        }

        #endregion
    }
}
