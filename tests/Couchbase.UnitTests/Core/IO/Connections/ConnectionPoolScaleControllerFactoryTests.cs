using System;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class ConnectionPoolScaleControllerFactoryTests
    {
        #region Create

        [Fact]
        public void Create_WithCustomIdleTimeout_SetOnController()
        {
            // Arrange

            var clusterOptions = new ClusterOptions
            {
                IdleKvConnectionTimeout = TimeSpan.FromMinutes(10)
            };

            var factory = new ConnectionPoolScaleControllerFactory(clusterOptions,
                new Mock<IRedactor>().Object,
                new Mock<ILogger<DefaultConnectionPoolScaleController>>().Object);

            // Act

            var result = factory.Create();

            // Assert

            var controller = Assert.IsType<DefaultConnectionPoolScaleController>(result);
            Assert.Equal(TimeSpan.FromMinutes(10), controller.IdleConnectionTimeout);
        }

        #endregion
    }
}
