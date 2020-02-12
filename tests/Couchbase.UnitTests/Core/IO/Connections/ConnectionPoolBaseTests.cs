using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class ConnectionPoolBaseTests
    {
        #region CreateConnectionAsync

        [Fact]
        public async Task CreateConnectionAsync_ReturnsFromFactory_WithInitializerEndpoint()
        {
            // Arrange

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(ipEndPoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(ipEndPoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object);

            // Act

            var result = await pool.CreateConnectionAsyncPublic();

            // Assert

            Assert.Equal(connection.Object, result);
        }

        [Fact]
        public async Task CreateConnectionAsync_CallsInitializer()
        {
            // Arrange

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(ipEndPoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(ipEndPoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object);

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

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(ipEndPoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(ipEndPoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object);

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

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(ipEndPoint);

            var connection = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();
            connectionFactory
                .Setup(m => m.CreateAndConnectAsync(ipEndPoint, It.IsAny<CancellationToken>()))
                .ReturnsAsync(connection.Object);

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object)
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

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(ipEndPoint);

            var connection1 = new Mock<IConnection>();
            var connection2 = new Mock<IConnection>();

            var connectionFactory = new Mock<IConnectionFactory>();

            var pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object)
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

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);

            var connectionInitializer = new Mock<IConnectionInitializer>();
            connectionInitializer
                .SetupGet(m => m.EndPoint)
                .Returns(ipEndPoint);
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

            pool = new ConnectionPoolMock(connectionInitializer.Object, connectionFactory.Object)
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

            public List<IConnection> Connections { get; } = new List<IConnection>();

            public bool IsFrozen { get; private set; }

            public ConnectionPoolMock(IConnectionInitializer connectionInitializer, IConnectionFactory connectionFactory)
                : base(connectionInitializer, connectionFactory)
            {
            }

            protected override ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default)
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
