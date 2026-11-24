using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.IO.Connections.Channels
{
    // NCBC-4111: Prevent parallel test execution to avoid resource contention and timing issues on CI
    [Collection("NonParallel")]
    public class ChannelConnectionPoolTests
    {
        // Bounds each wait, so a regression fails instead of hanging.
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        private readonly ITestOutputHelper _testOutput;
        private readonly HostEndpointWithPort _hostEndpoint = new("localhost", 9999);
        const int queueSize = 1024;

        public ChannelConnectionPoolTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        #region Diagnostics tracking

        // NCBC-4236: pools were tracked for the "send_queue_length" gauge in a static ConcurrentBag which
        // had no removal, so every pool ever created leaked an entry. Nodes are recreated on every config
        // change, so disposing a pool must untrack it.
        [Fact]
        public void Dispose_UntracksThePoolForDiagnostics()
        {
            var pool = CreatePool();

            Assert.True(pool.IsTrackedForDiagnostics);

            pool.Dispose();

            Assert.False(pool.IsTrackedForDiagnostics);
        }

        [Fact]
        public void Dispose_Twice_LeavesThePoolUntracked()
        {
            var pool = CreatePool();

            pool.Dispose();
            pool.Dispose();

            Assert.False(pool.IsTrackedForDiagnostics);
        }

        #endregion

        #region InitializeAsync

        // InitializeAsync returns at the first connection. The refill loop opens the rest while it holds the pool lock.
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public async Task InitializeAsync_MinimumSize_OpensThatNumber(int size)
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Mock<IConnection>().Object);

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = size;
            pool.MaximumSize = size;

            // Act

            await pool.InitializeAsync().WaitAsync(deadline.Token);

            // Assert

            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.Equal(size, pool.Size);
            }

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(size));
        }

        [Fact]
        public async Task InitializeAsync_SomeConnectionsFail_KeepsPartialPool()
        {
            // Arrange

            // A host is not always equally reachable on every connection attempt, so one connection can
            // succeed while a sibling times out. The node is still usable and must not be discarded.
            var attempt = 0;
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .Returns(() => Interlocked.Increment(ref attempt) == 1
                    ? Task.FromResult(new Mock<IConnection>().Object)
                    : Task.FromException<IConnection>(new SocketException((int) SocketError.TimedOut)));

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            // Act

            await pool.InitializeAsync();

            // Assert

            Assert.Equal(1, pool.Size);
        }

        [Fact]
        public async Task InitializeAsync_AllConnectionsFail_Throws()
        {
            // Arrange

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromException<IConnection>(new SocketException((int) SocketError.TimedOut)));

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            // Act / Assert

            // An empty pool is not a usable node, so this must still fail rather than be tolerated.
            await Assert.ThrowsAsync<SocketException>(() => pool.InitializeAsync());
            Assert.Equal(0, pool.Size);
        }

        [Fact]
        public async Task InitializeAsync_AllConnectionsDead_Throws()
        {
            // Arrange

            // A connection which comes back dead is dropped without throwing, so an empty pool can be
            // reached without any exception to catch. That is still not a usable node.
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connection.SetupGet(m => m.IsDead).Returns(true);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            // Act / Assert

            await Assert.ThrowsAsync<ConnectException>(() => pool.InitializeAsync());
            Assert.Equal(0, pool.Size);
        }

        [Fact]
        public async Task InitializeAsync_DeadConnection_IsDisposed()
        {
            // Arrange

            // A dead connection has already connected, so it owns a socket which nothing else will
            // close once the pool drops the reference.
            var connection = new Mock<IConnection>();
            connection.SetupGet(m => m.IsDead).Returns(true);

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => connection.Object);

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;

            // Act / Assert

            await Assert.ThrowsAsync<ConnectException>(() => pool.InitializeAsync());

            connection.Verify(m => m.Dispose(), Times.Once);
        }

        // The caller cancels while the first round connects. InitializeAsync throws even though a connection opens
        // later, and the loop stops after its round with no retry.
        [Fact]
        public async Task InitializeAsync_Cancelled_ThrowsEvenWithAWorkingConnection()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            using var cts = new CancellationTokenSource();
            var clock = new TimerRecordingTimeProvider();
            var working = new TaskCompletionSource<IConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempt = 0;

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .Returns(() => Interlocked.Increment(ref attempt) == 1
                    ? working.Task
                    : Task.FromException<IConnection>(new SocketException((int) SocketError.TimedOut)));

            var pool = CreatePool(connectionFactory: connectionFactory.Object, timeProvider: clock);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            // Act

            var initialize = pool.InitializeAsync(cts.Token);
            cts.Cancel();

            // Assert

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => initialize.WaitAsync(deadline.Token));

            working.SetResult(new Mock<IConnection>().Object);

            // The round ends under the pool lock, so the freeze waits for it.
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.Equal(1, pool.Size);
                Assert.False(pool.IsRefilling);
            }

            Assert.Equal(0, clock.TimersCreated);
            Assert.Equal(2, Volatile.Read(ref attempt));
        }

        [Fact]
        public async Task InitializeAsync_FirstRoundAuthenticationFails_ThrowsTheSameExceptionAndStops()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var clock = new TimerRecordingTimeProvider();
            var error = new AuthenticationFailureException("Bad credentials.");

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Mock<IConnection>().Object);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer.SetupGet(m => m.EndPoint).Returns(_hostEndpoint);
            connectionInitializer
                .Setup(m => m.InitializeConnectionAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromException(error));

            var pool = CreatePool(connectionInitializer.Object, connectionFactory.Object, clock);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            // Act

            // ClusterContext matches this exact type, so it must not be wrapped.
            var thrown = await Assert.ThrowsAsync<AuthenticationFailureException>(
                () => pool.InitializeAsync().WaitAsync(deadline.Token));

            // Assert

            Assert.Same(error, thrown);
            AssertNoConnectAfterTheFirstRound(pool, clock, connectionFactory, 2);
        }

        [Fact]
        public async Task InitializeAsync_FirstRoundTlsFails_ThrowsTheSameExceptionAndStops()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var clock = new TimerRecordingTimeProvider();
            var error = new System.Security.Authentication.AuthenticationException("The remote certificate is invalid.");

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromException<IConnection>(error));

            var pool = CreatePool(connectionFactory: connectionFactory.Object, timeProvider: clock);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            // Act

            // ClusterContext reads the message of this exception, so it must not be wrapped.
            var thrown = await Assert.ThrowsAsync<System.Security.Authentication.AuthenticationException>(
                () => pool.InitializeAsync().WaitAsync(deadline.Token));

            // Assert

            Assert.Same(error, thrown);
            AssertNoConnectAfterTheFirstRound(pool, clock, connectionFactory, 2);
        }

        [Fact]
        public async Task InitializeAsync_FirstRoundOnlyDeadConnections_ThrowsConnectExceptionAndStops()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var clock = new TimerRecordingTimeProvider();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new Mock<IConnection>();
                    connection.SetupGet(m => m.IsDead).Returns(true);
                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object, timeProvider: clock);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            // Act

            var thrown = await Assert.ThrowsAsync<ConnectException>(() => pool.InitializeAsync().WaitAsync(deadline.Token));

            // Assert

            Assert.Equal($"Unable to open any connections to {_hostEndpoint}.", thrown.Message);
            AssertNoConnectAfterTheFirstRound(pool, clock, connectionFactory, 2);
        }

        private void AssertNoConnectAfterTheFirstRound(ChannelConnectionPool pool, TimerRecordingTimeProvider clock,
            Mock<IConnectionFactory> connectionFactory, int firstRound)
        {
            // A loop that kept going would wait on a timer before its next attempt.
            Assert.False(pool.IsRefilling);
            Assert.Equal(0, clock.TimersCreated);
            clock.Advance(ChannelConnectionPool.MaxRefillDelay);

            Assert.Equal(0, pool.Size);
            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(firstRound));
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

        [Fact(Skip="Inconsistent behavior in underprovisioned Jenkins.")]
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
                if (currentSentCount == toSendCount)
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

        [Theory(Skip="Inconsistent behavior in underprovisioned Jenkins.")]
        [InlineData(2)]
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

            await using (await pool.FreezePoolAsync())
            {
                await pool.ScaleAsync(-2);
            }

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

        [Fact(Skip = "Randomly fails on underprovisioned EC2 Jenkins.")]
        public async Task Scale_Down_DoesNotWaitForClose()
        {
            // Arrange

            // Use TaskCompletionSource to track CloseAsync execution state.
            // CloseAsync is fire-and-forget in ScaleAsync, so we can't use simple boolean flags -
            // when ScaleAsync returns, the CloseAsync callback may not have started yet due to TPL scheduling.
            // RunContinuationsAsynchronously prevents deadlocks when TrySetResult is called from the callback.
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

            await using (await pool.FreezePoolAsync())
            {
                await pool.ScaleAsync(-1);
            }

            await Task.Delay(500);

            // Assert

            // Wait for CloseAsync to actually start executing. Since CloseAsync is fire-and-forget,
            // the TPL may not have scheduled the callback yet when ScaleAsync returns. Without this wait,
            // the test would intermittently fail when the scheduler was slower (e.g. on CI).
            var startedTask = await Task.WhenAny(closeStarted.Task, Task.Delay(TimeSpan.FromSeconds(30)));
            Assert.True(startedTask == closeStarted.Task && await closeStarted.Task,
                "CloseAsync should have started after ScaleAsync(-1) returned");

            // Verify CloseAsync hasn't completed, which proves ScaleAsync returned without waiting
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

            await using (await pool.FreezePoolAsync())
            {
                await pool.ScaleAsync(-1);
            }

            // Assert

            var closedConnections = connections
                .Select(p => p.Object)
                .Except(pool.GetConnections());

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

            await using (await pool.FreezePoolAsync())
            {
                await pool.ScaleAsync(-1);
            }

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

            await using (await pool.FreezePoolAsync())
            {
                await pool.ScaleAsync(-3);
            }

            // Assert

            Assert.Equal(pool.MinimumSize, pool.Size);

            var closedConnections = connections
                .Select(p => p.Object)
                .Except(pool.GetConnections())
                .ToList();

            Assert.Equal(2, closedConnections.Count);
            Assert.All(closedConnections, p => Assert.DoesNotContain(p, pool.GetConnections()));

            connectionFactory.Verify(
                m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()),
                Times.Exactly(4));
        }

        #endregion

        #region Dead connections

        // NCBC-4372: a connection the server closed while the pool was idle stayed in the pool until the
        // next queued operation woke its processor, which for an idle application could be days.
        [Fact]
        public async Task IdleConnection_ClosedByServer_IsReplacedWithoutAnyTraffic()
        {
            // Arrange

            var connections = new List<ControllableConnection>();
            var replacementCreated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new ControllableConnection();
                    lock (connections)
                    {
                        connections.Add(connection);
                        if (connections.Count == 3)
                        {
                            replacementCreated.TrySetResult(true);
                        }
                    }

                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;
            await pool.InitializeAsync();

            // Act

            var closed = connections[0];
            closed.CloseFromServer();

            // Assert

            await replacementCreated.Task;

            // The replacement is added while the pool is locked, so freezing waits for it to finish.
            await using (await pool.FreezePoolAsync())
            {
                var live = pool.GetConnections().ToList();
                Assert.DoesNotContain(closed.Object, live);
                Assert.Contains(connections[2].Object, live);
                Assert.Equal(2, live.Count);
            }
        }

        // TrySendImmediatelyAsync (used for config fetches) never sends on a dead connection. With no live connection it
        // fails at once. The pool replaces the dead connections without any traffic.
        [Fact]
        public async Task TrySendImmediatelyAsync_AllConnectionsDead_ThrowsAndReplacesThem()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var connections = new List<ControllableConnection>();
            var replacementsCreated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new ControllableConnection();
                    lock (connections)
                    {
                        connections.Add(connection);
                        if (connections.Count == 4)
                        {
                            replacementsCreated.TrySetResult(true);
                        }
                    }

                    return connection.Object;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;
            await pool.InitializeAsync();

            var dead = connections.ToList();
            var sentOn = new ConcurrentBag<IConnection>();
            var operation = new FakeOperation { SendStarted = sentOn.Add };

            // Act

            // The frozen pool cannot replace the connections yet, so the send finds only dead ones.
            Task<bool> send;
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                dead.ForEach(c => c.CloseFromServer());
                send = pool.TrySendImmediatelyAsync(operation);
            }

            // Assert

            await Assert.ThrowsAsync<SocketNotAvailableException>(() => send.WaitAsync(deadline.Token));
            Assert.Equal(0, pool.PendingSends);
            Assert.DoesNotContain(sentOn, c => dead.Any(d => d.Object == c));

            // Both processors leave once their connections die, and the pool replaces the connections.
            await replacementsCreated.Task.WaitAsync(deadline.Token);
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.All(pool.GetConnections(), c => Assert.DoesNotContain(dead, d => d.Object == c));
            }
        }

        // A node that left the cluster closes its connections and every reconnect fails, so the pool has no processor
        // to read its queue. A config fetch must fail at once, so its caller can try another node.
        [Fact]
        public async Task TrySendImmediatelyAsync_NoProcessorLeft_ThrowsAtOnceWithoutQueueing()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var connections = new List<ControllableConnection>();
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    lock (connections)
                    {
                        if (connections.Count < 2)
                        {
                            var connection = new ControllableConnection();
                            connections.Add(connection);
                            return Task.FromResult(connection.Object);
                        }
                    }

                    return Task.FromException<IConnection>(new SocketException((int) SocketError.ConnectionRefused));
                });

            var clock = new TimerRecordingTimeProvider();
            var pool = CreatePool(connectionFactory: connectionFactory.Object, timeProvider: clock);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;
            await pool.InitializeAsync();

            connections.ForEach(c => c.CloseFromServer());

            // Both processors left the pool, and the refill loop failed and waits for its next attempt.
            await Task.WhenAll(connections.Select(c => c.CloseCalled)).WaitAsync(deadline.Token);
            await clock.NextTimerAsync(deadline.Token);

            var operation = new FakeOperation();

            // Act

            // Refill attempts hold the pool lock while they connect, so the send must not wait for it.
            Task<bool> send;
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.Equal(0, pool.Size);
                send = pool.TrySendImmediatelyAsync(operation);
            }

            // Assert

            Assert.True(send.IsCompleted, "The send waited instead of failing at once.");
            await Assert.ThrowsAsync<SocketNotAvailableException>(() => send);
            Assert.Equal(0, pool.PendingSends);
        }

        // The config fetch sends here. Its token must reach the connection, so a busy connection cannot hold it past
        // its timeout.
        [Fact]
        public async Task TrySendImmediatelyAsync_PassesItsTokenToTheSend()
        {
            // Arrange

            var pool = CreatePool();
            await pool.InitializeAsync();

            using var cts = new CancellationTokenSource();
            var operation = new FakeOperation();

            // Act

            var sentImmediately = await pool.TrySendImmediatelyAsync(operation, cts.Token);

            // Assert

            Assert.True(sentImmediately);
            cts.Cancel();
            Assert.True(operation.SendToken.IsCancellationRequested, "The send did not get the caller's token.");
        }

        [Fact]
        public async Task SelectBucketAsync_NoLiveConnections_NamesTheRequestedBucket()
        {
            // Arrange

            var connection = new ControllableConnection();
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;
            await pool.InitializeAsync();
            connection.MarkDeadSilently();

            // Act

            var ex = await Assert.ThrowsAsync<DocumentNotFoundException>(() => pool.SelectBucketAsync("travel-sample"));

            // Assert

            Assert.Equal("Failed to select bucket travel-sample.", ex.Message);
        }

        // InitializeAsync returns at the first live connection. The refill loop keeps the pool lock until the sibling
        // is registered, so a bucket selection that freezes the pool sees every connection.
        [Fact]
        public async Task InitializeAsync_ReturnsAtTheFirstConnection_TheLoopHoldsThePoolLockUntilTheSiblingIsRegistered()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var secondConnectionSetup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pool = CreatePoolWithGatedSecondConnection(secondConnectionSetup.Task, new List<ControllableConnection>());

            // Act

            await pool.InitializeAsync().WaitAsync(deadline.Token);

            // Assert

            Assert.Equal(1, pool.Size);

            // A held lock returns an incomplete task at once, so no wait is needed to see it.
            var freeze = pool.FreezePoolAsync(deadline.Token).AsTask();
            Assert.False(freeze.IsCompleted);

            secondConnectionSetup.SetResult(true);
            await using (await freeze.WaitAsync(deadline.Token))
            {
                Assert.Equal(2, pool.Size);
            }
        }

        // A connection closes while its sibling still connects. The loop ends its round, sees the pool short of one
        // and opens a replacement.
        [Fact]
        public async Task InitializeAsync_ConnectionClosesWhileASiblingConnects_PoolEndsAtMinimumWithoutIt()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var connections = new List<ControllableConnection>();
            var secondConnectionSetup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pool = CreatePoolWithGatedSecondConnection(secondConnectionSetup.Task, connections,
                replacementCreated: out var replacementCreated);

            await pool.InitializeAsync().WaitAsync(deadline.Token);

            // Act

            // The first connection is registered and its sibling is still connecting.
            var closed = connections[0];
            closed.CloseFromServer();
            await closed.CloseCalled.WaitAsync(deadline.Token);
            secondConnectionSetup.SetResult(true);
            await replacementCreated.WaitAsync(deadline.Token);

            // Assert

            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                var live = pool.GetConnections().ToList();
                Assert.Equal(2, live.Count);
                Assert.DoesNotContain(closed.Object, live);
            }
        }

        private ChannelConnectionPool CreatePoolWithGatedSecondConnection(Task secondConnectionSetup,
            List<ControllableConnection> connections) =>
            CreatePoolWithGatedSecondConnection(secondConnectionSetup, connections, out _);

        /// <summary>
        /// A pool of exactly 2 whose second connection's setup waits for <paramref name="secondConnectionSetup"/>.
        /// <paramref name="replacementCreated"/> completes when a third connection is created.
        /// </summary>
        private ChannelConnectionPool CreatePoolWithGatedSecondConnection(Task secondConnectionSetup,
            List<ControllableConnection> connections, out Task replacementCreated)
        {
            var replacement = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            replacementCreated = replacement.Task;

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var connection = new ControllableConnection();
                    lock (connections)
                    {
                        connections.Add(connection);
                        if (connections.Count == 3)
                        {
                            replacement.TrySetResult(true);
                        }
                    }

                    return connection.Object;
                });

            var setups = 0;
            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer.SetupGet(m => m.EndPoint).Returns(_hostEndpoint);
            connectionInitializer
                .Setup(m => m.InitializeConnectionAsync(It.IsAny<IConnection>(), It.IsAny<CancellationToken>()))
                .Returns(() => Interlocked.Increment(ref setups) == 2 ? secondConnectionSetup : Task.CompletedTask);

            var pool = CreatePool(connectionInitializer.Object, connectionFactory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;
            return pool;
        }

        /// <summary>
        /// A connection whose death the test controls: either signalled through <see cref="IConnection.ClosedToken"/>,
        /// as a real connection does, or silent, so its processor keeps waiting and the pool keeps it.
        /// </summary>
        private sealed class ControllableConnection
        {
            private readonly CancellationTokenSource _closed = new();
            private readonly TaskCompletionSource<bool> _closeCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _disposeCalled = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private volatile bool _dead;

            public ControllableConnection()
            {
                var mock = new Mock<IConnection>();
                mock.SetupGet(m => m.ClosedToken).Returns(_closed.Token);
                mock.SetupGet(m => m.IsDead).Returns(() => _dead);
                mock.Setup(m => m.CloseAsync(It.IsAny<TimeSpan>()))
                    .Callback(() => _closeCalled.TrySetResult(true))
                    .Returns(default(ValueTask));
                mock.Setup(m => m.Dispose()).Callback(() =>
                {
                    _dead = true;
                    _disposeCalled.TrySetResult(true);
                });
                Object = mock.Object;
            }

            public IConnection Object { get; }

            /// <summary>
            /// Completes when the processor closes the connection on its way out, after it left the pool.
            /// </summary>
            public Task CloseCalled => _closeCalled.Task;

            public Task DisposeCalled => _disposeCalled.Task;

            public void CloseFromServer()
            {
                _dead = true;
                _closed.Cancel();
            }

            public void MarkDeadSilently() => _dead = true;
        }

        #endregion

        [Fact]
        public void Factory_ResolvedFromTheClusterServices_GetsItsClock()
        {
            using var context = new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"));

            Assert.IsType<ChannelConnectionPoolFactory>(context.ServiceProvider.GetRequiredService<IConnectionPoolFactory>());
        }

        #region Bucket at creation

        [Fact]
        public async Task BucketPool_EachNewConnection_SelectsTheBucket()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var factory = new RefillFactory();
            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer.SetupGet(m => m.EndPoint).Returns(_hostEndpoint);

            var pool = CreatePool(connectionInitializer.Object, factory.Object, bucketName: "travel-sample");
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;
            await pool.InitializeAsync().WaitAsync(deadline.Token);

            // Act

            await factory.ConnectionCreated(2).WaitAsync(deadline.Token);
            factory.Connections[0].CloseFromServer();
            await factory.ConnectionCreated(3).WaitAsync(deadline.Token);

            // Assert

            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.Contains(factory.Connections[2].Object, pool.GetConnections());
            }

            Assert.All(factory.Connections, connection => connectionInitializer.Verify(
                m => m.SelectBucketAsync(connection.Object, "travel-sample", It.IsAny<CancellationToken>()),
                Times.Once));
        }

        [Fact]
        public async Task PoolWithNoBucket_NewConnections_SelectNoBucket()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var factory = new RefillFactory();
            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer.SetupGet(m => m.EndPoint).Returns(_hostEndpoint);

            var pool = CreatePool(connectionInitializer.Object, factory.Object);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;
            await pool.InitializeAsync().WaitAsync(deadline.Token);

            // Act

            await factory.ConnectionCreated(2).WaitAsync(deadline.Token);
            factory.Connections[0].CloseFromServer();
            await factory.ConnectionCreated(3).WaitAsync(deadline.Token);

            // Assert

            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.Contains(factory.Connections[2].Object, pool.GetConnections());
            }

            connectionInitializer.Verify(
                m => m.SelectBucketAsync(It.IsAny<IConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region Refill

        // A pool under its minimum keeps trying, so it does not wait for the scale controller tick.
        [Fact]
        public async Task Refill_ReplacementFails_RetriesWithABackoffUntilItConnects()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var clock = new TimerRecordingTimeProvider();
            var factory = new RefillFactory();
            var pool = CreatePool(connectionFactory: factory.Object, timeProvider: clock);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;
            await pool.InitializeAsync().WaitAsync(deadline.Token);

            // Act

            factory.Failing = true;
            factory.Connections[0].CloseFromServer();

            // Assert

            var expected = new[] { 32, 64, 128, 256, 512, 1024, 2048, 4096, 4096 };
            for (var i = 0; i < expected.Length; i++)
            {
                var due = await clock.NextTimerAsync(deadline.Token);
                Assert.Equal(TimeSpan.FromMilliseconds(expected[i]), due);

                if (i == expected.Length - 1)
                {
                    factory.Failing = false;
                }

                clock.Advance(due);
            }

            await factory.ConnectionCreated(2).WaitAsync(deadline.Token);
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.Same(factory.Connections[1].Object, Assert.Single(pool.GetConnections()));
            }

            Assert.Equal(expected.Length, clock.TimersCreated);
        }

        // A node that refuses the bucket makes the select dispose the connection, which comes back dead with no error.
        [Fact]
        public async Task Refill_NodeRefusesTheBucket_RetriesUntilItAccepts()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var clock = new TimerRecordingTimeProvider();
            var factory = new RefillFactory();
            var refuse = false;
            var accepted = 0;

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer.SetupGet(m => m.EndPoint).Returns(_hostEndpoint);
            connectionInitializer
                .Setup(m => m.SelectBucketAsync(It.IsAny<IConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((IConnection connection, string _, CancellationToken _) =>
                {
                    if (Volatile.Read(ref refuse))
                    {
                        factory.Find(connection).MarkDeadSilently();
                    }
                    else
                    {
                        Interlocked.Increment(ref accepted);
                    }

                    return Task.CompletedTask;
                });

            var pool = CreatePool(connectionInitializer.Object, factory.Object, clock);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;
            await pool.InitializeAsync().WaitAsync(deadline.Token);
            await pool.SelectBucketAsync("travel-sample").WaitAsync(deadline.Token);

            // Act

            Volatile.Write(ref refuse, true);
            var closed = factory.Connections[0];
            closed.CloseFromServer();

            // Assert

            Assert.Equal(TimeSpan.FromMilliseconds(32), await clock.NextTimerAsync(deadline.Token));
            clock.Advance(TimeSpan.FromMilliseconds(32));
            Assert.Equal(TimeSpan.FromMilliseconds(64), await clock.NextTimerAsync(deadline.Token));

            Volatile.Write(ref refuse, false);
            clock.Advance(TimeSpan.FromMilliseconds(64));

            await factory.ConnectionCreated(5).WaitAsync(deadline.Token);
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                var live = pool.GetConnections().ToList();
                Assert.Equal(2, live.Count);
                Assert.DoesNotContain(closed.Object, live);
                Assert.All(live, c => Assert.False(c.IsDead));
            }

            Assert.Equal(3, Volatile.Read(ref accepted));
            Assert.Equal(2, clock.TimersCreated);
        }

        [Fact]
        public async Task Refill_AfterARecovery_TheBackoffStartsAgainAt32Ms()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var clock = new TimerRecordingTimeProvider();
            var factory = new RefillFactory();
            var pool = CreatePool(connectionFactory: factory.Object, timeProvider: clock);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;
            await pool.InitializeAsync().WaitAsync(deadline.Token);

            factory.Failing = true;
            factory.Connections[0].CloseFromServer();
            Assert.Equal(TimeSpan.FromMilliseconds(32), await clock.NextTimerAsync(deadline.Token));
            clock.Advance(TimeSpan.FromMilliseconds(32));
            Assert.Equal(TimeSpan.FromMilliseconds(64), await clock.NextTimerAsync(deadline.Token));
            factory.Failing = false;
            clock.Advance(TimeSpan.FromMilliseconds(64));
            await factory.ConnectionCreated(2).WaitAsync(deadline.Token);

            // Act

            factory.Failing = true;
            factory.Connections[1].CloseFromServer();

            // Assert

            Assert.Equal(TimeSpan.FromMilliseconds(32), await clock.NextTimerAsync(deadline.Token));
        }

        [Fact]
        public async Task Refill_SeveralConnectionsCloseTogether_OneLoopRuns()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var factory = new RefillFactory();
            var pool = CreatePool(connectionFactory: factory.Object);
            pool.MinimumSize = 3;
            pool.MaximumSize = 3;
            await pool.InitializeAsync().WaitAsync(deadline.Token);
            var initialLoops = pool.RefillStarts;

            // Act

            // The frozen pool holds the loop back, so every removal happens while it waits.
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                var closing = factory.Connections.ToList();
                closing.ForEach(c => c.CloseFromServer());
                await Task.WhenAll(closing.Select(c => c.CloseCalled)).WaitAsync(deadline.Token);

                // Assert

                Assert.Equal(initialLoops + 1, pool.RefillStarts);
            }

            await factory.ConnectionCreated(6).WaitAsync(deadline.Token);
            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                Assert.Equal(3, pool.Size);
                Assert.Equal(initialLoops + 1, pool.RefillStarts);
            }
        }

        // A round where every connect works can still end under the minimum, when a connection closes meanwhile.
        [Fact]
        public async Task Refill_ConnectionsKeepClosingWhileASiblingConnects_BacksOffAfterOneRetryAtOnce()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var clock = new TimerRecordingTimeProvider();
            var factory = new GatedFactory();
            var pool = CreatePool(connectionFactory: factory.Object, timeProvider: clock);
            pool.MinimumSize = 2;
            pool.MaximumSize = 2;

            var initialize = pool.InitializeAsync();
            var first = await factory.OpenAsync(1, deadline.Token);
            await initialize.WaitAsync(deadline.Token);

            // Act

            first.CloseFromServer();
            await first.CloseCalled.WaitAsync(deadline.Token);
            var second = await factory.OpenAsync(2, deadline.Token);

            // The first round ended with 1 of 2 and no failed connect, so the loop connects again at once.
            await factory.Started(3).WaitAsync(deadline.Token);
            Assert.Equal(0, clock.TimersCreated);

            second.CloseFromServer();
            await second.CloseCalled.WaitAsync(deadline.Token);
            var third = await factory.OpenAsync(3, deadline.Token);

            // Assert

            Assert.Equal(TimeSpan.FromMilliseconds(32), await clock.NextTimerAsync(deadline.Token));
            Assert.Equal(3, factory.StartedCount);

            clock.Advance(TimeSpan.FromMilliseconds(32));
            var fourth = await factory.OpenAsync(4, deadline.Token);

            await using (await pool.FreezePoolAsync(deadline.Token))
            {
                var live = pool.GetConnections().ToList();
                Assert.Equal(2, live.Count);
                Assert.Contains(third.Object, live);
                Assert.Contains(fourth.Object, live);
            }

            Assert.Equal(1, clock.TimersCreated);
        }

        // A refill attempt holds the pool lock while it connects. Dispose must not wait for it.
        [Fact]
        public async Task Dispose_DuringARefillAttempt_ReturnsAtOnceAndDisposesTheLateConnection()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var first = new ControllableConnection();
            var late = new ControllableConnection();
            var lateConnect = new TaskCompletionSource<IConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            var lateConnectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempt = 0;

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (Interlocked.Increment(ref attempt) == 1)
                    {
                        return Task.FromResult(first.Object);
                    }

                    // Ignores the token, as a TLS handshake on .NET Framework does.
                    lateConnectStarted.TrySetResult(true);
                    return lateConnect.Task;
                });

            var pool = CreatePool(connectionFactory: connectionFactory.Object);
            pool.MinimumSize = 1;
            pool.MaximumSize = 1;
            await pool.InitializeAsync().WaitAsync(deadline.Token);

            first.CloseFromServer();
            await lateConnectStarted.Task.WaitAsync(deadline.Token);

            // Act

            await Task.Run(pool.Dispose).WaitAsync(deadline.Token);

            // Assert

            lateConnect.SetResult(late.Object);
            await late.DisposeCalled.WaitAsync(deadline.Token);
            Assert.Equal(0, pool.Size);
        }

        // Readers take no lock, so a connection added meanwhile must never show a half built list.
        [Fact]
        public async Task Readers_WhileConnectionsAreAddedAndRemoved_SeeAWholeArray()
        {
            // Arrange

            using var deadline = new CancellationTokenSource(Deadline);
            var pool = CreatePool();
            pool.MinimumSize = 1;
            pool.MaximumSize = 2;
            await pool.InitializeAsync().WaitAsync(deadline.Token);

            var reads = 0;
            var stop = 0;
            var readerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var reader = Task.Run(async () =>
            {
                readerStarted.TrySetResult(true);
                while (Volatile.Read(ref stop) == 0)
                {
                    Assert.All(pool.GetConnections(), Assert.NotNull);
                    Assert.True(await pool.TrySendImmediatelyAsync(new FakeOperation()));
                    Interlocked.Increment(ref reads);
                }
            });

            await readerStarted.Task.WaitAsync(deadline.Token);

            // Act

            while (Volatile.Read(ref reads) < 2000 && !reader.IsCompleted)
            {
                await using (await pool.FreezePoolAsync(deadline.Token))
                {
                    await pool.ScaleAsync(1).WaitAsync(deadline.Token);
                    await pool.ScaleAsync(-1).WaitAsync(deadline.Token);
                }
            }

            Volatile.Write(ref stop, 1);

            // Assert

            await reader.WaitAsync(deadline.Token);
        }

        /// <summary>
        /// Starts each connect when the pool asks and completes it when the test calls <see cref="OpenAsync"/>.
        /// </summary>
        private sealed class GatedFactory
        {
            private readonly Mock<IConnectionFactory> _mock = new();
            private readonly List<TaskCompletionSource<IConnection>> _connects = new();
            private readonly List<(int Count, TaskCompletionSource<bool> Signal)> _waiters = new();

            public GatedFactory()
            {
                _mock
                    .Setup(m => m.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        var connect = new TaskCompletionSource<IConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                        lock (_connects)
                        {
                            _connects.Add(connect);
                            foreach (var waiter in _waiters)
                            {
                                if (_connects.Count >= waiter.Count)
                                {
                                    waiter.Signal.TrySetResult(true);
                                }
                            }
                        }

                        return connect.Task;
                    });
            }

            public IConnectionFactory Object => _mock.Object;

            public int StartedCount
            {
                get
                {
                    lock (_connects)
                    {
                        return _connects.Count;
                    }
                }
            }

            /// <summary>
            /// Completes when the pool has started <paramref name="count"/> connects.
            /// </summary>
            public Task Started(int count)
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_connects)
                {
                    if (_connects.Count >= count)
                    {
                        signal.TrySetResult(true);
                    }
                    else
                    {
                        _waiters.Add((count, signal));
                    }
                }

                return signal.Task;
            }

            /// <summary>
            /// Waits for connect number <paramref name="number"/> (from 1) and completes it with a new connection.
            /// </summary>
            public async Task<ControllableConnection> OpenAsync(int number, CancellationToken cancellationToken)
            {
                await Started(number).WaitAsync(cancellationToken);

                var connection = new ControllableConnection();
                TaskCompletionSource<IConnection> connect;
                lock (_connects)
                {
                    connect = _connects[number - 1];
                }

                connect.SetResult(connection.Object);
                return connection;
            }
        }

        /// <summary>
        /// Creates <see cref="ControllableConnection"/>s, or fails while <see cref="Failing"/> is set.
        /// </summary>
        private sealed class RefillFactory
        {
            private readonly Mock<IConnectionFactory> _mock = new();
            private readonly List<(int Count, TaskCompletionSource<bool> Signal)> _waiters = new();
            private volatile bool _failing;

            public RefillFactory()
            {
                _mock
                    .Setup(m => m.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                    .Returns(() =>
                    {
                        if (_failing)
                        {
                            return Task.FromException<IConnection>(new SocketException((int) SocketError.ConnectionRefused));
                        }

                        var connection = new ControllableConnection();
                        lock (Connections)
                        {
                            Connections.Add(connection);
                            foreach (var waiter in _waiters)
                            {
                                if (Connections.Count >= waiter.Count)
                                {
                                    waiter.Signal.TrySetResult(true);
                                }
                            }
                        }

                        return Task.FromResult(connection.Object);
                    });
            }

            public IConnectionFactory Object => _mock.Object;

            public List<ControllableConnection> Connections { get; } = new();

            public bool Failing
            {
                get => _failing;
                set => _failing = value;
            }

            /// <summary>
            /// Completes when the factory has created <paramref name="count"/> connections.
            /// </summary>
            public Task ConnectionCreated(int count)
            {
                var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (Connections)
                {
                    if (Connections.Count >= count)
                    {
                        signal.TrySetResult(true);
                    }
                    else
                    {
                        _waiters.Add((count, signal));
                    }
                }

                return signal.Task;
            }

            public ControllableConnection Find(IConnection connection)
            {
                lock (Connections)
                {
                    return Connections.Single(c => c.Object == connection);
                }
            }
        }

        #endregion

        #region Helpers

        private ChannelConnectionPool CreatePool(IConnectionInitializer connectionInitializer = null,
            IConnectionFactory connectionFactory = null, TimeProvider timeProvider = null, string bucketName = null)
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

            return new ChannelConnectionPool(connectionInitializer, connectionFactory,
                new Mock<IConnectionPoolScaleController>().Object,
                new Redactor(RedactionLevel.None),
                new Logger(_testOutput),
                timeProvider ?? new FakeTimeProvider(),
                (int) new ClusterOptions().KvSendQueueCapacity,
                bucketName);
        }

        private class FakeOperation : OperationBase
        {
            public TimeSpan Delay { get; set; } = TimeSpan.Zero;

            public Action<IConnection> SendStarted { get; set; }

            public Action<IConnection> SendComplete { get; set; }

            public CancellationToken SendToken { get; private set; }

            public override OpCode OpCode => OpCode.NoOp;

            public override async Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
            {
                SendToken = cancellationToken;
                SendStarted?.Invoke(connection);

                if (Delay > TimeSpan.Zero)
                {
                    await Task.Delay(Delay, cancellationToken);
                }

                SendComplete?.Invoke(connection);
            }
        }

        private class Logger : ILogger<ChannelConnectionPool>
        {
            private readonly ITestOutputHelper _testOutput;

            public Logger(ITestOutputHelper testOutput)
            {
                _testOutput = testOutput ?? throw new ArgumentNullException(nameof(testOutput));
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                try
                {
                    _testOutput.WriteLine(formatter(state, exception));
                }
                catch (InvalidOperationException)
                {
                    // Background pool work can log after the test has finished.
                }
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
