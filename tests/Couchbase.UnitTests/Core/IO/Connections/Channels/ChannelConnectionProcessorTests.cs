using Couchbase.Core.Diagnostics.Tracing;
using Couchbase;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net;
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

        // A graceful close marks the connection dead at once, but drains the operations in flight for up to a minute.
        // The processor must leave when the close starts, so the pool replaces the connection during the drain.
        [Fact]
        public async Task GracefulClose_WithAnOperationInFlight_ReplacesTheConnectionWhileItDrains()
        {
            // Arrange

            using var stream = new FeedStream();
            var connection = new MultiplexingConnection(stream, 8, new IPEndPoint(0, 0), new IPEndPoint(0, 0),
                NullLogger<MultiplexingConnection>.Instance);

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

            var processor = new ChannelConnectionProcessor(connection, pool, sendQueue, new Logger(_testOutput));
            var process = processor.Process();

            var operation = new Noop();
            await connection.SendAsync(new byte[HeaderOffsets.HeaderLength], operation);
            var completed = operation.Completed.AsTask();

            // Act

            var close = connection.CloseAsync(TimeSpan.FromMinutes(1)).AsTask();

            // Assert

            await process.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(new[] { replacement }, pool.GetConnections());

            // The processor also calls CloseAsync on its way out. That does not cut the drain.
            Assert.False(close.IsCompleted);
            Assert.False(stream.Disposed.IsCompleted);

            stream.Feed(Response(operation.Opaque));
            Assert.Equal(ResponseStatus.Success, await completed.WaitAsync(TimeSpan.FromSeconds(10)));
            await close.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(stream.Disposed.IsCompleted);
        }

        private static byte[] Response(uint opaque)
        {
            var packet = new byte[HeaderOffsets.HeaderLength];
            packet[HeaderOffsets.Magic] = (byte) Magic.ClientResponse;
            packet[HeaderOffsets.Opcode] = (byte) OpCode.NoOp;
            // The SDK writes the opaque little-endian and the server echoes it unchanged.
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(HeaderOffsets.Opaque), opaque);
            return packet;
        }
    }
}
