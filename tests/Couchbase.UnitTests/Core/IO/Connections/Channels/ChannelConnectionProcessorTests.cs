using Couchbase.Core.Diagnostics.Tracing;
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
                new Mock<IRedactor>().Object,
                new Logger(_testOutput),
                (int)new ClusterOptions().KvSendQueueCapacity,
                channel
            );
        }


        private class ChannelProcessorFakeOperation : OperationBase
        {
            public override OpCode OpCode => throw new NotImplementedException();

            public override async Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
            {
                await connection.SendAsync(Memory<byte>.Empty, new Noop(), cancellationToken).ConfigureAwait(false);
            }
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
    }
}
