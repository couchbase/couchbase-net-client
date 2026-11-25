using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using BucketNotFoundException = Couchbase.Management.Buckets.BucketNotFoundException;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class SingleConnectionPoolTests
    {
        // Bounds each wait, so a regression fails instead of hanging.
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        private const string Bucket = "travel-sample";

        private readonly HostEndpointWithPort _hostEndpoint = new("localhost", 9999);
        private readonly List<IConnection> _created = new();
        private volatile bool _refuse = true;
        private int _attempts;

        // Holds each connect until it completes, when set.
        private TaskCompletionSource<bool> _gate;

        [Fact]
        public async Task SendAsync_AfterARefusedFirstConnect_ConnectsAndSends()
        {
            var pool = CreatePool();
            await Assert.ThrowsAsync<BucketNotFoundException>(() => pool.InitializeAsync().WaitAsync(Deadline));

            _refuse = false;
            var op = new FakeOperation();
            await pool.SendAsync(op).WaitAsync(Deadline);

            Assert.Equal(2, Created.Count);
            Assert.Same(Created[1], op.SentOn);
        }

        [Fact]
        public async Task SendAsync_ReconnectRefused_ThrowsSocketNotAvailableAndTheNextSendTriesAgain()
        {
            var pool = CreatePool();
            await Assert.ThrowsAsync<BucketNotFoundException>(() => pool.InitializeAsync().WaitAsync(Deadline));

            await Assert.ThrowsAsync<SocketNotAvailableException>(() => pool.SendAsync(new FakeOperation()).WaitAsync(Deadline));
            Assert.Equal(2, Created.Count);

            await Assert.ThrowsAsync<SocketNotAvailableException>(() => pool.SendAsync(new FakeOperation()).WaitAsync(Deadline));
            Assert.Equal(3, Created.Count);
        }

        [Fact]
        public async Task SendAsync_DisposedPool_ThrowsSocketNotAvailableAndOpensNoConnection()
        {
            _refuse = false;
            var pool = CreatePool();
            await pool.InitializeAsync().WaitAsync(Deadline);

            pool.Dispose();

            var op = new FakeOperation();
            await Assert.ThrowsAsync<SocketNotAvailableException>(() => pool.SendAsync(op).WaitAsync(Deadline));
            Assert.Single(Created);
            Assert.Null(op.SentOn);
        }

        // NCBC-4423. A bucket node that refused the bucket joins with no connection, so sends can all find none.
        // Each send starts its connect before its first await, so the count is final before the gate opens.
        [Fact]
        public async Task SendAsync_ConcurrentSendsWithNoConnection_OpenOneConnection()
        {
            _refuse = false;
            _gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pool = CreatePool();

            var ops = new List<FakeOperation>();
            var sends = new List<Task>();
            for (var i = 0; i < 5; i++)
            {
                var op = new FakeOperation();
                ops.Add(op);
                sends.Add(pool.SendAsync(op));
            }

            Assert.Equal(1, Volatile.Read(ref _attempts));

            _gate.SetResult(true);
            await Task.WhenAll(sends).WaitAsync(Deadline);

            var connection = Assert.Single(Created);
            Assert.All(ops, op => Assert.Same(connection, op.SentOn));
        }

        [Fact]
        public async Task Dispose_DuringAConnect_LeavesNoOpenConnection()
        {
            _refuse = false;
            _gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pool = CreatePool();
            var connect = pool.InitializeAsync();
            Assert.Equal(1, Volatile.Read(ref _attempts));

            pool.Dispose();
            _gate.SetResult(true);

            await Assert.ThrowsAsync<SocketNotAvailableException>(() => connect.WaitAsync(Deadline));
            Mock.Get(Assert.Single(Created)).Verify(c => c.Dispose(), Times.Once);
            Assert.Empty(pool.GetConnections());
        }

        private List<IConnection> Created
        {
            get
            {
                lock (_created)
                {
                    return new List<IConnection>(_created);
                }
            }
        }

        private SingleConnectionPool CreatePool()
        {
            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    Interlocked.Increment(ref _attempts);
                    if (_gate is { } gate)
                    {
                        await gate.Task.ConfigureAwait(false);
                    }

                    var connection = new Mock<IConnection>().Object;
                    lock (_created)
                    {
                        _created.Add(connection);
                    }

                    return connection;
                });

            // Refuses the bucket while _refuse is set, as ClusterNode does on KeyNotFound.
            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer.SetupGet(m => m.EndPoint).Returns(_hostEndpoint);
            connectionInitializer
                .Setup(m => m.SelectBucketAsync(It.IsAny<IConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((IConnection _, string bucketName, CancellationToken _) => _refuse
                    ? Task.FromException(new BucketNotFoundException(bucketName))
                    : Task.CompletedTask);

            return new SingleConnectionPool(connectionInitializer.Object, connectionFactory.Object,
                NullLogger<IConnectionPool>.Instance, Bucket);
        }

        private sealed class FakeOperation : OperationBase
        {
            public IConnection SentOn { get; private set; }

            public override OpCode OpCode => OpCode.NoOp;

            public override Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
            {
                SentOn = connection;
                return Task.CompletedTask;
            }
        }
    }
}
