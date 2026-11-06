using Couchbase.Core.Diagnostics.Tracing;
using Couchbase;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.IO.Connections.Channels
{
    public class ChannelConnectionProcessorTests
    {
        private readonly ITestOutputHelper _testOutput;
        private readonly HostEndpointWithPort _hostEndpoint = new("localhost", 9999);
        const int queueSize = 1024;

        public ChannelConnectionProcessorTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
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


        private ChannelConnectionPool CreatePool(Channel<ChannelQueueItem> channel, IConnectionInitializer connectionInitializer = null,
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

            return new ChannelConnectionPool(connectionInitializer, connectionFactory,
                new Mock<IConnectionPoolScaleController>().Object,
                new Redactor(RedactionLevel.None),
                new Logger(_testOutput),
                channel
            );
        }

        private class ChannelProcessorFakeOperation : OperationBase
        {
            public override OpCode OpCode => throw new NotImplementedException();

            public override async Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
            {
                await connection.SendAsync(Memory<byte>.Empty, new Noop(), cancellationToken);
            }
        }

        [Fact]
        public async Task Should_CloseConnection_When_RemoveConnectionAsyncThrowsException()
        {
            var connectionMock = new Mock<IConnection>();
            var connectionFactoryMock = new Mock<IConnectionFactory>();

            var sendQueue = Channel.CreateBounded<ChannelQueueItem>(new BoundedChannelOptions(3)
            {
                AllowSynchronousContinuations = true
            });

            connectionMock
                .SetupGet(e => e.IsDead)
                .Returns(true);

            connectionFactoryMock
                .Setup(e => e.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception());

            var pool = CreatePool(sendQueue, connectionFactory: connectionFactoryMock.Object);

            var processor = new ChannelConnectionProcessor(connectionMock.Object, pool, sendQueue, new Logger(_testOutput));
            await pool.SendAsync(new ChannelProcessorFakeOperation() { }, CancellationToken.None);
            await processor.Process();

            connectionMock
                .Verify(e => e.CloseAsync(It.IsAny<TimeSpan>()), times: Times.Once);
        }

        [Fact]
        public async Task Should_SendRequest_When_PreviousRequestsAreCancelled()
        {
            var connectionMock = new Mock<IConnection>();

            var sendQueue = Channel.CreateBounded<ChannelQueueItem>(new BoundedChannelOptions(3)
            {
                AllowSynchronousContinuations = true
            });

            var pool = CreatePool(sendQueue);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


            var processor = new ChannelConnectionProcessor(connectionMock.Object, pool, sendQueue, new Logger(_testOutput));

            await pool.SendAsync(new ChannelProcessorFakeOperation() { }, cancellationTokenSource.Token);
            await pool.SendAsync(new ChannelProcessorFakeOperation() { }, CancellationToken.None);
            await pool.SendAsync(new ChannelProcessorFakeOperation() { }, cancellationTokenSource.Token);

            cancellationTokenSource.Cancel();
            sendQueue.Writer.TryComplete();

            await processor.Process();

            connectionMock
                .Verify(e => e.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IOperation>(), It.IsAny<CancellationToken>()), times: Times.Once);
        }

        // A connection in a graceful close is dead before its ClosedToken fires, so only the wake makes the processor exit.
        [Fact]
        public async Task WakeIfConnectionDead_DeadConnection_ProcessorExitsAndThePoolReplacesIt()
        {
            // Arrange

            var connectionMock = new Mock<IConnection>();
            connectionMock
                .SetupGet(e => e.IsDead)
                .Returns(true);

            var replacement = new Mock<IConnection>().Object;
            var connectionFactoryMock = new Mock<IConnectionFactory>();
            connectionFactoryMock
                .Setup(e => e.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(replacement);

            var sendQueue = Channel.CreateBounded<ChannelQueueItem>(new BoundedChannelOptions(3)
            {
                AllowSynchronousContinuations = true
            });

            var pool = CreatePool(sendQueue, connectionFactory: connectionFactoryMock.Object);
            pool.MinimumSize = 1;

            var processor = new ChannelConnectionProcessor(connectionMock.Object, pool, sendQueue, new Logger(_testOutput));
            var process = processor.Process();

            // Act

            processor.WakeIfConnectionDead();

            // Assert

            Assert.Same(process, await Task.WhenAny(process, Task.Delay(TimeSpan.FromSeconds(5))));
            Assert.True(processor.IsComplete);
            connectionMock
                .Verify(e => e.CloseAsync(It.IsAny<TimeSpan>()), times: Times.Once);

            // The pool replaces a connection only when its processor removes itself.
            Assert.Equal(new[] { replacement }, pool.GetConnections());

            // A late wake, as from a pool that copied its list before the removal, is harmless.
            processor.WakeIfConnectionDead();
        }

        [Fact]
        public async Task WakeIfConnectionDead_LiveConnection_ProcessorKeepsSending()
        {
            // Arrange

            var sent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var connectionMock = new Mock<IConnection>();
            connectionMock
                .Setup(e => e.SendAsync(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<IOperation>(), It.IsAny<CancellationToken>()))
                .Callback(() => sent.TrySetResult(true))
                .Returns(default(ValueTask));

            var connectionFactoryMock = new Mock<IConnectionFactory>();

            var sendQueue = Channel.CreateBounded<ChannelQueueItem>(new BoundedChannelOptions(3)
            {
                AllowSynchronousContinuations = true
            });

            var pool = CreatePool(sendQueue, connectionFactory: connectionFactoryMock.Object);

            var processor = new ChannelConnectionProcessor(connectionMock.Object, pool, sendQueue, new Logger(_testOutput));
            _ = processor.Process();

            // Act

            processor.WakeIfConnectionDead();

            // Assert

            // Only a running processor reads the queue.
            await pool.SendAsync(new ChannelProcessorFakeOperation());
            Assert.Same(sent.Task, await Task.WhenAny(sent.Task, Task.Delay(TimeSpan.FromSeconds(5))));
            Assert.False(processor.IsComplete);
            connectionFactoryMock
                .Verify(e => e.CreateAndConnectAsync(It.IsAny<HostEndpointWithPort>(), It.IsAny<CancellationToken>()), times: Times.Never);

            await processor.CompleteAsync();
        }
    }
}
