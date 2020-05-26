using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class DefaultConnectionPoolScaleControllerTests
    {
        #region Start

        [Fact]
        public void Start_NullConnectionPool_ArgumentNullException()
        {
            // Arrange

            var controller = new MockController();

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => controller.Start(null));
        }

        [Fact]
        public void Start_Disposed_ObjectDisposedException()
        {
            // Arrange

            var connectionPool = new Mock<IConnectionPool>();

            var controller = new MockController();

            controller.Dispose();

            // Act/Assert

            Assert.Throws<ObjectDisposedException>(() => controller.Start(connectionPool.Object));
        }

        [Fact]
        public async Task Start_WithConnectionPool_StartsMonitor()
        {
            // Arrange

            var connectionPool = new Mock<IConnectionPool>();

            var tcs = new TaskCompletionSource<bool>();

            var controller = new Mock<MockController>
            {
                CallBase = true
            };
            controller.Protected()
                .Setup<Task>("MonitorAsync")
                .Callback(() => { tcs.SetResult(true); })
                .Returns(Task.CompletedTask);

            // Act

            controller.Object.Start(connectionPool.Object);
            await tcs.Task;

            // Assert

            controller.Protected()
                .Verify("MonitorAsync", Times.Once());
        }

        #endregion

        #region RunScalingLogic

        [Fact]
        public async Task RunScalingLogic_AtMinimumSize_NoChange()
        {
            // Arrange

            var connectionPool = CreateMockConnectionPool(
                2, 2, 5, 0,
                Enumerable.Range(1, 2).Select(_ => CreateMockConnection(TimeSpan.Zero)));

            var controller = new MockController();

            // Act

            await controller.RunScalingLogicPublic(connectionPool.Object);

            // Assert

            connectionPool.Verify(
                m => m.ScaleAsync(It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task RunScalingLogic_BelowMinimumSize_ScaleUp()
        {
            // Arrange

            var connectionPool = CreateMockConnectionPool(
                0, 2, 5, 0,
                Enumerable.Range(1, 1).Select(_ => CreateMockConnection(TimeSpan.Zero)));

            var controller = new MockController
            {
                BackPressureThreshold = 4
            };

            // Act

            await controller.RunScalingLogicPublic(connectionPool.Object);

            // Assert

            connectionPool.Verify(
                m => m.ScaleAsync(1),
                Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(59)]
        public async Task RunScalingLogic_AllConnectionsBelowTimeout_NoChange(int lastActivitySeconds)
        {
            // Arrange

            var connectionPool = CreateMockConnectionPool(
                2, 2, 5, 0,
                Enumerable.Range(1, 2).Select(_ => CreateMockConnection(TimeSpan.FromSeconds(lastActivitySeconds))));

            var controller = new MockController
            {
                IdleConnectionTimeout = TimeSpan.FromSeconds(60)
            };

            // Act

            await controller.RunScalingLogicPublic(connectionPool.Object);

            // Assert

            connectionPool.Verify(
                m => m.ScaleAsync(It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task RunScalingLogic_OneConnectionAboveTimeout_ScaleDown()
        {
            // Arrange

            var connectionPool = CreateMockConnectionPool(
                5, 2, 5, 0,
                Enumerable.Range(1, 5).Select(i =>
                    CreateMockConnection(i < 5 ? TimeSpan.Zero : TimeSpan.FromSeconds(60))));

            var controller = new MockController
            {
                IdleConnectionTimeout = TimeSpan.FromSeconds(60)
            };

            // Act

            await controller.RunScalingLogicPublic(connectionPool.Object);

            // Assert

            connectionPool.Verify(
                m => m.ScaleAsync(-1),
                Times.Once);
        }

        [Fact]
        public async Task RunScalingLogic_AtMaximumSize_NoChange()
        {
            // Arrange

            var connectionPool = CreateMockConnectionPool(
                5, 2, 5, 1000,
                Enumerable.Range(1, 2).Select(_ => CreateMockConnection(TimeSpan.Zero)));

            var controller = new MockController();

            // Act

            await controller.RunScalingLogicPublic(connectionPool.Object);

            // Assert

            connectionPool.Verify(
                m => m.ScaleAsync(It.IsAny<int>()),
                Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task RunScalingLogic_AtOrBelowBackPressureThreshold_NoChange(int backPressure)
        {
            // Arrange

            var connectionPool = CreateMockConnectionPool(
                5, 2, 5, backPressure,
                Enumerable.Range(1, 2).Select(_ => CreateMockConnection(TimeSpan.Zero)));

            var controller = new MockController
            {
                BackPressureThreshold = 4
            };

            // Act

            await controller.RunScalingLogicPublic(connectionPool.Object);

            // Assert

            connectionPool.Verify(
                m => m.ScaleAsync(It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task RunScalingLogic_AboveBackPressureThreshold_ScaleUp()
        {
            // Arrange

            var connectionPool = CreateMockConnectionPool(
                2, 2, 5, 5,
                Enumerable.Range(1, 2).Select(_ => CreateMockConnection(TimeSpan.Zero)));

            var controller = new MockController
            {
                BackPressureThreshold = 4
            };

            // Act

            await controller.RunScalingLogicPublic(connectionPool.Object);

            // Assert

            connectionPool.Verify(
                m => m.ScaleAsync(1),
                Times.Once);
        }

        #endregion

        #region Helpers

        private static Mock<IConnectionPool> CreateMockConnectionPool(
            int size, int minimumSize, int maximumSize, int backPressure, IEnumerable<IConnection> connections)
        {
            var connectionPool = new Mock<IConnectionPool>();
            connectionPool
                .SetupGet(m => m.Size)
                .Returns(size);
            connectionPool
                .SetupGet(m => m.MinimumSize)
                .Returns(minimumSize);
            connectionPool
                .SetupGet(m => m.MaximumSize)
                .Returns(maximumSize);
            connectionPool
                .SetupGet(m => m.PendingSends)
                .Returns(backPressure);
            connectionPool
                .Setup(m => m.GetConnections())
                .Returns(connections);

            return connectionPool;
        }

        private static IConnection CreateMockConnection(TimeSpan idleTime)
        {
            var connection = new Mock<IConnection>();
            connection
                .SetupGet(m => m.IdleTime)
                .Returns(idleTime);

            return connection.Object;
        }

        internal class MockController : DefaultConnectionPoolScaleController
        {
            public MockController()
                : base(new Mock<IRedactor>().Object, new Mock<ILogger<DefaultConnectionPoolScaleController>>().Object)
            {
            }

            protected override Task MonitorAsync()
            {
                return Task.CompletedTask;
            }

            public Task RunScalingLogicPublic(IConnectionPool connectionPool)
            {
                return RunScalingLogic(connectionPool);
            }
        }

        #endregion
    }
}
