using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class ConnectionPoolBaseTests
    {
        private readonly HostEndpointWithPort _hostEndpoint = new("localhost", 9999);

        #region GetSendQueueLength

        // The gauge is the sum of PendingSends across every tracked pool, so a single pool's queue is not
        // the whole story and the aggregation itself is worth pinning down.
        [Fact]
        public void GetSendQueueLength_SumsAcrossPools()
        {
            var registry = new WeakInstanceRegistry<ConnectionPoolBase>();
            var first = new ConnectionPoolMock(2);
            var second = new ConnectionPoolMock(3);
            var third = new ConnectionPoolMock(5);

            registry.Add(first);
            registry.Add(second);
            registry.Add(third);

            Assert.Equal(10, ConnectionPoolBase.GetSendQueueLength(registry));

            GC.KeepAlive(first);
            GC.KeepAlive(second);
            GC.KeepAlive(third);
        }

        [Fact]
        public void GetSendQueueLength_IsZero_WhenNoPoolsAreTracked()
        {
            var registry = new WeakInstanceRegistry<ConnectionPoolBase>();

            Assert.Equal(0, ConnectionPoolBase.GetSendQueueLength(registry));
        }

        // This is what the fix changes in observable terms: a disposed pool untracks itself, so its queue
        // stops being counted rather than lingering in the gauge for the life of the process.
        [Fact]
        public void GetSendQueueLength_ExcludesRemovedPools()
        {
            var registry = new WeakInstanceRegistry<ConnectionPoolBase>();
            var kept = new ConnectionPoolMock(2);
            var removed = new ConnectionPoolMock(7);

            registry.Add(kept);
            var id = registry.Add(removed);

            Assert.Equal(9, ConnectionPoolBase.GetSendQueueLength(registry));

            registry.Remove(id);

            Assert.Equal(2, ConnectionPoolBase.GetSendQueueLength(registry));

            GC.KeepAlive(kept);
            GC.KeepAlive(removed);
        }

        // A pool abandoned without being disposed leaves a dead weak reference behind. Reading the gauge
        // must skip it rather than throwing or counting it as zero-and-still-present.
        [Fact]
        public void GetSendQueueLength_SkipsCollectedPools()
        {
            var registry = new WeakInstanceRegistry<ConnectionPoolBase>();
            var live = new ConnectionPoolMock(4);

            registry.AddWeak(new WeakReference<ConnectionPoolBase>(null!));
            registry.Add(live);

            Assert.Equal(4, ConnectionPoolBase.GetSendQueueLength(registry));

            GC.KeepAlive(live);
        }

        #endregion

        #region CreateConnectionAsync

        [Fact]
        public async Task CreateConnectionAsync_ReturnsFromFactory_WithInitializerEndpoint()
        {
            // Arrange

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(_hostEndpoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object, new Mock<ILogger<IConnectionPool>>().Object);

            // Act

            var result = await pool.CreateConnectionAsyncPublic();

            // Assert

            Assert.Equal(connection.Object, result);
        }

        [Fact]
        public async Task CreateConnectionAsync_CallsInitializer()
        {
            // Arrange

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(_hostEndpoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object, new Mock<ILogger<IConnectionPool>>().Object);

            // Act

            var result = await pool.CreateConnectionAsyncPublic();

            // Assert

            connectionInitializer.Verify(
                m => m.InitializeConnectionAsync(connection.Object, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateConnectionAsync_NoBucket_DoesNotSelectBucket()
        {
            // Arrange

            var ipEndPoint = new HostEndpointWithPort("127.0.0.1", 9999);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(ipEndPoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(ipEndPoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object, new Mock<ILogger<IConnectionPool>>().Object);

            // Act

            var result = await pool.CreateConnectionAsyncPublic();

            // Assert

            connectionInitializer.Verify(
                m => m.SelectBucketAsync(It.IsAny<IConnection>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CreateConnectionAsync_HasBucket_SelectBucket()
        {
            // Arrange

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(_hostEndpoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(_hostEndpoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object, new Mock<ILogger<IConnectionPool>>().Object)
            {
                BucketNamePublic = "default"
            };

            // Act

            var result = await pool.CreateConnectionAsyncPublic();

            // Assert

            connectionInitializer.Verify(
                m => m.SelectBucketAsync(connection.Object, "default", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region SelectBucketAsync

        [Fact]
        public async Task SelectBucketAsync_CallsForEachConnection()
        {
            // Arrange

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(_hostEndpoint);

            var connection1 = new Mock<IConnection>();
            var connection2 = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object, new Mock<ILogger<IConnectionPool>>().Object)
            {
                Connections =
                {
                    connection1.Object,
                    connection2.Object
                }
            };

            // Act

            await pool.SelectBucketAsync("default");

            // Assert

            Assert.All(new[] {connection1.Object, connection2.Object},
                connection => connectionInitializer.Verify(
                    m => m.SelectBucketAsync(connection, "default", It.IsAny<CancellationToken>()),
                    Times.Once()));
        }

        [Fact]
        public async Task SelectBucketAsync_FreezesDuringOperation()
        {
            // Arrange

            ConnectionPoolMock pool = null;

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(_hostEndpoint);
            connectionInitializer
                .Setup(m => m.SelectBucketAsync(It.IsAny<IConnection>(), "default", It.IsAny<CancellationToken>()))
                .Returns((IConnection connection, string bucket, CancellationToken _) =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    Assert.True(pool?.IsFrozen ?? false);
                    return Task.CompletedTask;
                });

            var connection1 = new Mock<IConnection>();
            var connection2 = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();

            pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object, new Mock<ILogger<IConnectionPool>>().Object)
            {
                Connections =
                {
                    connection1.Object,
                    connection2.Object
                }
            };

            // Act

            await pool.SelectBucketAsync("default");

            // Assert

            Assert.All(new[] {connection1.Object, connection2.Object},
                connection => connectionInitializer.Verify(
                    m => m.SelectBucketAsync(connection, "default", It.IsAny<CancellationToken>()),
                    Times.Once()));

            Assert.False(pool.IsFrozen);
        }

        #endregion

        private class ConnectionPoolMock : ConnectionPoolBase
        {
            public string BucketNamePublic
            {
                get => BucketName;
                set => BucketName = value;
            }

            public override int Size => 1;
            public override int MinimumSize { get; set; }
            public override int MaximumSize { get; set; }
            public override int PendingSends { get; }

            public ConnectionPoolMock(int pendingSends)
                : this(CreateConnectionInitializer(), Mock.Of<IConnectionFactory>(),
                    Mock.Of<ILogger<IConnectionPool>>())
            {
                PendingSends = pendingSends;
            }

            private static IConnectionInitializer CreateConnectionInitializer()
            {
                var mock = new Mock<IConnectionInitializer>();
                mock.SetupGet(m => m.EndPoint).Returns(new HostEndpointWithPort("localhost", 9999));
                return mock.Object;
            }

            public List<IConnection> Connections { get; } = new List<IConnection>();

            public bool IsFrozen { get; private set; }

            public ConnectionPoolMock(IConnectionInitializer connectionInitializer, IConnectionFactory connectionFactory, ILogger<IConnectionPool> logger)
                : base(connectionInitializer, connectionFactory, logger)
            {
            }

            public override ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default)
            {
                IsFrozen = true;

                return new ValueTask<IAsyncDisposable>(new Frozen(this));
            }

            public override Task InitializeAsync(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public override Task SendAsync(IOperation operation, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public override IEnumerable<IConnection> GetConnections()
            {
                return Connections;
            }

            public override Task ScaleAsync(int delta)
            {
                throw new NotImplementedException();
            }

            public override void Dispose()
            {
            }

            public Task<IConnection> CreateConnectionAsyncPublic()
            {
                return CreateConnectionAsync(default);
            }

            private class Frozen : IAsyncDisposable
            {
                private readonly ConnectionPoolMock _mock;

                public Frozen(ConnectionPoolMock mock)
                {
                    _mock = mock;
                }

                public ValueTask DisposeAsync()
                {
                    _mock.IsFrozen = false;

                    return default;
                }
            }
        }
    }
}
