using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.DataFlow;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.IO.Connections.DataFlow
{
    [Collection("NonParallel")]
    public class DataFlowConnectionPoolTests
    {
        private readonly ITestOutputHelper _testOutput;
        private readonly HostEndpointWithPort _hostEndpoint = new("localhost", 9999);
        const int queueSize = 1024;

        public DataFlowConnectionPoolTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        #region InitializeAsync

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public async Task InitializeAsync_MinimumSize_OpensThatNumber(int size)
        {
            // Arrange

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Mock<IConnection>().Object);

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = size;
            pool.MaximumSize = size;

            // Act

            await pool.InitializeAsync();

            // Assert

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(size));
        }

        #endregion


        #region SendAsync

        [Fact]
        public async Task SendAsync_SingleOp_IsSent()
        {
            // Arrange

            var tcs = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource(10000); // prevent wait forever
            cts.Token.Register(() => tcs.TrySetResult(false));  // set result to false on timeout
            var pool = CreatePool();
            await pool.InitializeAsync();

            var operation = new FakeOperation
            {
                SendStarted = _ => tcs.TrySetResult(true)
            };

            // Act

            await pool.SendAsync(operation);

            // Assert

            Assert.True(await tcs.Task, "Send was not started before timeout");
        }

        [Fact]
        public async Task SendAsync_QueueFull_ThrowsSendQueueFullException()
        {
            // Arrange
            var connection = new Mock<IConnection>();
            var connectionFactory = new Mock<IConnectionFactory>();
            connection.Setup(m => m.IsDead).Returns(false);
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => connection.Object);

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;

            await pool.InitializeAsync();

            // Act
            // Assert
            await Assert.ThrowsAsync<SendQueueFullException>(async () => {
                // Send 2 more operations than queueSize to fill up the queue
                // since the fake op has a delay, at most one op will be dequeued before queue full exception
                for (int i = 0; i < queueSize + 2; i++)
                {
                    await pool.SendAsync(new FakeOperation() { Delay = TimeSpan.FromSeconds(3) });
                }
            });
        }

        [Fact]
        public async Task SendAsync_QueueFull_After_CleaningUpDeadConnections_SetsExceptionOnOperation()
        {
            // Arrange
            int count = 0;
            var connection = new Mock<IConnection>();
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => connection.Object);

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;
            connection.Setup(m => m.IsDead).Returns(() => {
                count++;
                // The first time IsDead is checked is during pool.InitializeAsync
                // The second time is done after dequeueing the first op
                if (count == 2)
                {
                    // Stop processing further messages, simulating a full send queue
                    pool.Stop();

                    return true; // simulate dead connection
                }
                return false;
            });
            await pool.InitializeAsync();

            var operation = new FakeOperation() { Delay = TimeSpan.FromSeconds(3), Cid = 1 };

            // Act

            // queue the operation we expect to be requeued after cleanup
            await pool.SendAsync(operation);

            // wait for operation to fail but not forever
            await Task.WhenAny(operation.Completed.AsTask(), Task.Delay(3000));

            // Assert
            Assert.True(operation.Completed.IsCompleted);
            await Assert.ThrowsAsync<SendQueueFullException>(() => operation.Completed.AsTask());
        }

        [Fact]
        public async Task SendAsync_SingleConnection_NotSentSimultaneously()
        {
            // Arrange

            var pool = CreatePool();
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;

            await pool.InitializeAsync();

            var toSendCount = 10;
            long inProgressCount = 0;
            long maxInProgressCount = 0;
            long totalSentCount = 0;
            var tcs = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            cts.Token.Register(() => tcs.TrySetResult(false)); // set result to false on timeout

            void SendStarted(IConnection _)
            {
                var currentInProgress = Interlocked.Increment(ref inProgressCount);

                // NCBC-4111: Thread-safe update of maxInProgressCount using Interlocked.CompareExchange.
                long initialMax, computedMax;
                do
                {
                    initialMax = Interlocked.Read(ref maxInProgressCount);
                    computedMax = Math.Max(initialMax, currentInProgress);
                } while (initialMax != Interlocked.CompareExchange(ref maxInProgressCount, computedMax, initialMax));
            }

            void SendCompleted(IConnection _)
            {
                Interlocked.Decrement(ref inProgressCount);
                var currentSentCount = Interlocked.Increment(ref totalSentCount);
                if (currentSentCount  == toSendCount)
                    tcs.TrySetResult(true);
            }

            var operations = Enumerable.Range(1, toSendCount)
                .Select(_ => new FakeOperation
                {
                    Delay = TimeSpan.FromMilliseconds(100),
                    SendStarted = SendStarted,
                    SendComplete = SendCompleted
                })
                .ToList();

            // Act

            var tasks = operations.Select(p => pool.SendAsync(p)).ToList();

            // Assert

            Assert.True(await tcs.Task, "All sends were not completed before timeout");
            // Use Interlocked.Read for memory barrier consistency with the Interlocked writes above.
            // With MaxDegreeOfParallelism=1 in DataflowConnectionPool, maxInProgressCount should never exceed 1.
            Assert.Equal(1, Interlocked.Read(ref maxInProgressCount));
            Assert.Equal(0, Interlocked.Read(ref inProgressCount));
        }

        [Theory]
        [InlineData(4)]
        public async Task SendAsync_MultipleConnections_SentSimultaneously(int connections)
        {
            // Arrange

            var pool = CreatePool();
            pool.MinimumSize = connections;
            pool.MaximumSize = connections;

            await pool.InitializeAsync();

            var toSendCount = 10;
            var lockObject = new object();
            var inProgressCount = 0;
            var maxInProgressCount = 0;
            var totalSentCount = 0;
            var tcs = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource(10000); // prevent wait forever
            cts.Token.Register(() => tcs.TrySetResult(false));  // set result to false on timeout

            void SendStarted(IConnection _)
            {
                lock (lockObject)
                {
                    inProgressCount++;
                    maxInProgressCount = Math.Max(maxInProgressCount, inProgressCount);
                }
            }

            void SendCompleted(IConnection _)
            {
                lock (lockObject)
                {
                    inProgressCount--;
                    totalSentCount++;
                    if (totalSentCount == toSendCount)
                        tcs.TrySetResult(true);
                }
            }

            var operations = Enumerable.Range(1, toSendCount)
                .Select(_ => new FakeOperation
                {
                    Delay = TimeSpan.FromMilliseconds(100),
                    SendStarted = SendStarted,
                    SendComplete = SendCompleted
                })
                .ToList();

            // Act

            var tasks = operations.Select(p => pool.SendAsync(p)).ToList();

            // Assert

            Assert.True(await tcs.Task, "All sends were not started before timeout");
            Assert.Equal(connections, maxInProgressCount);
            Assert.Equal(0, inProgressCount);
        }

        [Fact]
        public async Task SendAsync_DeadConnection_ReplacesConnectionAndStillSends()
        {
            // Arrange

            var connectionCount = 0ul;
            var tcs = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource(10000); // prevent wait forever
            cts.Token.Register(() => tcs.TrySetResult(false));  // set result to false on timeout

            var connectionFactoryMock = new Mock<IConnectionFactory>();
            connectionFactoryMock
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    connectionCount++;

                    var connection = new Mock<IConnection>();
                    if (connectionCount == 1)
                    {
                        // First connection is dead
                        connection
                            .SetupGet(m => m.IsDead)
                            .Returns(true);
                    }

                    connection
                        .SetupGet(m => m.ConnectionId)
                        .Returns(connectionCount);

                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactoryMock.Object);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;

            await pool.InitializeAsync();

            var operationConnectionId = 0ul;
            var operation = new FakeOperation
            {
                SendStarted = connection =>
                {
                    operationConnectionId = connection.ConnectionId;
                    tcs.TrySetResult(true);
                }
            };

            // Act

            await pool.SendAsync(operation);
            Assert.True(await tcs.Task, "Send was not started before timeout");

            // Assert

            Assert.Equal(2ul, connectionCount);
            Assert.Equal(2ul, operationConnectionId);
        }

        [Fact]
        public async Task SendAsync_SendHasException_OperationExceptionIsSet()
        {
            // Arrange

            var tcs = new TaskCompletionSource<bool>();
            var cts = new CancellationTokenSource(10000); // prevent wait forever
            cts.Token.Register(() => tcs.TrySetResult(false));  // set result to false on timeout
            var pool = CreatePool();
            await pool.InitializeAsync();

            var operation = new FakeOperation
            {
                SendStarted = _ => throw new InvalidOperationException("testing")
            };

            // Act

            await pool.SendAsync(operation);

            // Assert

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.Completed.AsTask());
            Assert.Equal("testing", ex.Message);
        }

        #endregion

        #region Dispose

        [Fact]
        public async Task Dispose_ClosesAllConnections()
        {
            // Arrange

            var connectionCount = 0ul;
            var disposed = new HashSet<ulong>();

            var connectionFactoryMock = new Mock<IConnectionFactory>();
            connectionFactoryMock
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connectionId = ++connectionCount;

                    var connection = new Mock<IConnection>();
                    connection
                        .SetupGet(m => m.ConnectionId)
                        .Returns(connectionId);
                    connection
                        .Setup(m => m.Dispose())
                        .Callback(() => disposed.Add(connectionId));

                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactoryMock.Object);
            pool.MinimumSize = 5;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();

            // Act

            pool.Dispose();

            // Assert

            Assert.All(
                Enumerable.Range(1, 5),
                p => Assert.Contains((ulong)p, disposed));
        }

        #endregion

        #region Scale

        [Fact]
        public async Task Scale_Zero_DoesNothing()
        {
            // Arrange

            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();

            // Act

            await pool.ScaleAsync(0);

            // Assert

            Assert.Equal(pool.MinimumSize, pool.Size);
            Assert.All(connections, p => p.Verify(m => m.CloseAsync(It.IsAny<TimeSpan>()), Times.Never));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(pool.MinimumSize));
        }

        [Fact]
        public async Task Scale_Up_AddsConnections()
        {
            // Arrange

            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();

            // Act

            await pool.ScaleAsync(2);

            // Assert

            Assert.Equal(4, pool.Size);
            Assert.All(connections, p => p.Verify(m => m.CloseAsync(It.IsAny<TimeSpan>()), Times.Never));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(4));
        }

        [Fact]
        public async Task Scale_UpAtMax_DoesNothing()
        {
            // Arrange

            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            await pool.InitializeAsync();

            // Act

            await pool.ScaleAsync(1);

            // Assert

            Assert.Equal(pool.MaximumSize, pool.Size);
            Assert.All(connections, p => p.Verify(m => m.CloseAsync(It.IsAny<TimeSpan>()), Times.Never));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(pool.MaximumSize));
        }

        [Fact]
        public async Task Scale_UpMoreThanMax_ScalesToMax()
        {
            // Arrange

            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();

            // Act

            await pool.ScaleAsync(4);

            // Assert

            Assert.Equal(pool.MaximumSize, pool.Size);
            Assert.All(connections, p => p.Verify(m => m.CloseAsync(It.IsAny<TimeSpan>()), Times.Never));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(pool.MaximumSize));
        }

        [Fact]
        public async Task Scale_Down_DropsConnections()
        {
            // Arrange

            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();
            await pool.ScaleAsync(3);

            // Act

            await pool.ScaleAsync(-2);

            // Assert

            Assert.Equal(3, pool.Size);

            var closedConnections = connections
                .Where(p => p.Invocations.Any(q => q.Method == typeof(IConnection).GetMethod("CloseAsync")))
                .Select(p => p.Object)
                .ToList();

            Assert.Equal(2, closedConnections.Count);
            Assert.All(closedConnections, p => Assert.DoesNotContain(p, pool.GetConnections()));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(5));
        }

        [Fact]
        public async Task Scale_Down_DoesNotWaitForClose()
        {
            // Arrange

            // NCBC-4111: Adding a TaskCompletionSource to track the CloseAsync execution state.
            // CloseAsync is called as a fire-and-forget in ScaleAsync:
            // ---------------------------
            // #pragma warning disable 4014
            // // Don't wait for close, let it happen in the background
            // p.connection.Connection.CloseAsync(TimeSpan.FromMinutes(1));
            // #pragma warning restore 4014
            // ---------------------------
            // So when ScaleAsync returns, the CloseAsync callback "may not" have started yet due to the Task scheduling.
            // We can use RunContinuationsAsynchronously which prevents deadlocks when TrySetResult is called from the callback.
            var closeStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var closeCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connection.Setup(m => m.CloseAsync(It.IsAny<TimeSpan>()))
                        .Callback(async (TimeSpan _) =>
                        {
                            closeStarted.TrySetResult(true);
                            // Use a long delay (30s) to ensure the test can check closeCompleted
                            // before it finishes, even on slow CI machines
                            await Task.Delay(TimeSpan.FromSeconds(30));
                            closeCompleted.TrySetResult(true);
                        });
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();
            await pool.ScaleAsync(1);

            // Act

            Assert.False(closeStarted.Task.IsCompleted, "CloseAsync should not have started before ScaleAsync(-1)");
            Assert.False(closeCompleted.Task.IsCompleted, "CloseAsync should not have completed before ScaleAsync(-1)");

            await pool.ScaleAsync(-1);

            // Assert

            // Follow-up on NCBC-4111: Without this wait,
            // the test could intermittently fail on slow Jenkins runners
            var startedTask = await Task.WhenAny(closeStarted.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(startedTask == closeStarted.Task && await closeStarted.Task,
                "CloseAsync should have started after ScaleAsync(-1) returned");

            // Verify CloseAsync hasn't completed - this proves ScaleAsync returned without waiting
            Assert.False(closeCompleted.Task.IsCompleted,
                "CloseAsync should not have completed yet (ScaleAsync should not wait for close)");
        }

        [Fact]
        public async Task Scale_Down_ClosesLongestIdleFirst()
        {
            // Arrange

            var connectionCount = 0;
            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();

                    connectionCount++;
                    connection
                        .SetupGet(m => m.IdleTime)
                        .Returns(TimeSpan.FromMinutes(connectionCount));

                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();
            await pool.ScaleAsync(1);

            // Act

            await pool.ScaleAsync(-1);

            // Assert

            var closedConnections = connections
                .Where(p => p.Invocations.Any(q => q.Method == typeof(IConnection).GetMethod("CloseAsync")))
                .Select(p => p.Object);

            var closedConnection = Assert.Single(closedConnections);
            Assert.NotNull(closedConnection);

            Assert.Equal(TimeSpan.FromMinutes(3), closedConnection.IdleTime);
        }

        [Fact]
        public async Task Scale_DownAtMin_DoesNothing()
        {
            // Arrange

            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            await pool.InitializeAsync();

            // Act

            await pool.ScaleAsync(-1);

            // Assert

            Assert.Equal(pool.MinimumSize, pool.Size);
            Assert.All(connections, p => p.Verify(m => m.CloseAsync(It.IsAny<TimeSpan>()), Times.Never));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(pool.MinimumSize));
        }

        [Fact]
        public async Task Scale_DownMoreThanMin_ScalesToMin()
        {
            // Arrange

            var connections = new ConcurrentBag<Mock<IConnection>>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connections.Add(connection);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 5;

            await pool.InitializeAsync();
            await pool.ScaleAsync(2);

            // Act

            await pool.ScaleAsync(-3);

            // Assert

            Assert.Equal(pool.MinimumSize, pool.Size);

            var closedConnections = connections
                .Where(p => p.Invocations.Any(q => q.Method == typeof(IConnection).GetMethod("CloseAsync")))
                .Select(p => p.Object)
                .ToList();

            Assert.Equal(2, closedConnections.Count);
            Assert.All(closedConnections, p => Assert.DoesNotContain(p, pool.GetConnections()));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(4));
        }

        #endregion

        #region Helpers

        private class StoppableDataFlowConnectionPool : IConnectionPool
        {
            private DataFlowConnectionPool _innerPool;

            public StoppableDataFlowConnectionPool(IConnectionInitializer connectionInitializer, IConnectionFactory connectionFactory,
                IConnectionPoolScaleController scaleController, IRedactor redactor, ILogger<DataFlowConnectionPool> logger,
                uint kvSendQueueCapacity)
            {
                _innerPool = new(connectionInitializer, connectionFactory, scaleController, redactor, logger, kvSendQueueCapacity);
            }

            public HostEndpointWithPort EndPoint => _innerPool.EndPoint;

            public int Size => _innerPool.Size;

            public int MinimumSize
            {
                get => _innerPool.MinimumSize;
                set => _innerPool.MinimumSize = value;
            }

            public int MaximumSize
            {
                get => _innerPool.MaximumSize;
                set => _innerPool.MaximumSize = value;
            }

            public int PendingSends => _innerPool.PendingSends;

            public void Dispose()
            {
                _innerPool.Dispose();
            }

            public ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default)
            {
                return _innerPool.FreezePoolAsync(cancellationToken);
            }

            public IEnumerable<IConnection> GetConnections()
            {
                return _innerPool.GetConnections();
            }

            public Task InitializeAsync(CancellationToken cancellationToken = default)
            {
                return _innerPool.InitializeAsync(cancellationToken);
            }

            public Task ScaleAsync(int delta)
            {
                return _innerPool.ScaleAsync(delta);
            }

            public Task SelectBucketAsync(string name, CancellationToken cancellationToken = default)
            {
                return _innerPool.SelectBucketAsync(name, cancellationToken);
            }

            public Task SendAsync(IOperation op, CancellationToken cancellationToken = default)
            {
                return _innerPool.SendAsync(op, cancellationToken);
            }

            public void Stop() => _innerPool.CompleteSendQueue();

            public Task<bool> TrySendImmediatelyAsync(IOperation op, CancellationToken cancellationToken = default)
            {
                return _innerPool.TrySendImmediatelyAsync(op, cancellationToken);
            }
        }

        private StoppableDataFlowConnectionPool CreatePool(IConnectionInitializer connectionInitializer = null,
            IConnectionFactory connectionFactory = null)
        {
            if (connectionInitializer == null)
            {
                var connectionInitializerMock = new Mock<IConnectionInitializer>();
                connectionInitializerMock
                    .SetupGet(m => m.EndPoint)
                    .Returns(_hostEndpoint);
                connectionInitializer = connectionInitializerMock.Object;
            }

            if (connectionFactory == null)
            {
                var connectionFactoryMock = new Mock<IConnectionFactory>();
                connectionFactoryMock
                    .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => new Mock<IConnection>().Object);

                connectionFactory = connectionFactoryMock.Object;
            }

            return new StoppableDataFlowConnectionPool(connectionInitializer, connectionFactory,
                new Mock<IConnectionPoolScaleController>().Object,
                new Mock<IRedactor>().Object,
                new Logger(_testOutput),
                new ClusterOptions().KvSendQueueCapacity);
        }

        private class FakeOperation : OperationBase
        {
            public TimeSpan Delay { get; set; } = TimeSpan.Zero;

            public Action<IConnection> SendStarted { get; set; }

            public Action<IConnection> SendComplete { get; set; }

            public override OpCode OpCode => OpCode.NoOp;

            public override async Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
            {
                SendStarted?.Invoke(connection);

                if (Delay > TimeSpan.Zero)
                {
                    await Task.Delay(Delay);
                }

                SendComplete?.Invoke(connection);
            }
        }

        private class Logger : ILogger<DataFlowConnectionPool>
        {
            private readonly ITestOutputHelper _testOutput;

            public Logger(ITestOutputHelper testOutput)
            {
                _testOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _testOutput.WriteLine(formatter(state, exception));
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }

        #endregion
    }
}
