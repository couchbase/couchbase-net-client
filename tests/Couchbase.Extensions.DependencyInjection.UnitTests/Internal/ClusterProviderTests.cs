using System;
using System.Reflection;
using System.Threading.Tasks;
using Couchbase.Extensions.DependencyInjection.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests.Internal
{
    public class ClusterProviderTests
    {

        #region ctor

        [Fact]
        public void ctor_NoOptions_Exception()
        {
            // Act/Assert

            var loggerFactory = new Mock<ILoggerFactory>().Object;

            var ex = Assert.Throws<ArgumentNullException>(() => new ClusterProvider(null, loggerFactory));

            Assert.Equal("options", ex.ParamName);
        }

        [Fact]
        public void ctor_NoLoggerFactory_Exception()
        {
            // Arrange

            var options = new Mock<IOptions<ClusterOptions>>();

            // Act/Assert

            var ex = Assert.Throws<ArgumentNullException>(() => new ClusterProvider(options.Object, null));

            Assert.Equal("loggerFactory", ex.ParamName);
        }

        #endregion

        #region GetCluster

        [Fact]
        public async Task GetClusterAsync_Disposed_Exception()
        {
            // Arrange

            var options = new Mock<IOptions<ClusterOptions>>();
            var loggerFactory = new Mock<ILoggerFactory>().Object;

            var provider = new ClusterProvider(options.Object, loggerFactory);
            provider.Dispose();

            // Act/Assert

            var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.GetClusterAsync().AsTask());

            Assert.Equal(nameof(ClusterProvider), ex.ObjectName);
        }

        [Fact]
        public async Task GetCluster_FirstCall_ReturnsNewCluster()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptions<ClusterOptions>>();
            options.SetupGet(m => m.Value).Returns(clusterOptions);

            var loggerFactory = new Mock<ILoggerFactory>().Object;

            var cluster = new Mock<ICluster>();

            var provider = new Mock<ClusterProvider>(options.Object, loggerFactory)
            {
                CallBase = true
            };
            provider.Protected()
                .Setup<Task<ICluster>>("CreateClusterAsync", clusterOptions)
                .Returns(Task.FromResult(cluster.Object));

            // Act

            var result = await provider.Object.GetClusterAsync();

            // Assert

            Assert.Equal(cluster.Object, result);
        }

        [Fact]
        public async Task GetCluster_TwoCalls_OnlyCreatesOneCluster()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptions<ClusterOptions>>();
            options.SetupGet(m => m.Value).Returns(clusterOptions);

            var loggerFactory = new Mock<ILoggerFactory>().Object;

            var cluster = new Mock<ICluster>();

            var provider = new Mock<ClusterProvider>(options.Object, loggerFactory)
            {
                CallBase = true
            };
            provider.Protected()
                .Setup<Task<ICluster>>("CreateClusterAsync", clusterOptions)
                .Returns(Task.FromResult(cluster.Object));

            // Act

            await provider.Object.GetClusterAsync();
            var result = await provider.Object.GetClusterAsync();

            // Assert

            Assert.Equal(cluster.Object, result);

            provider.Protected()
                .Verify<Task<ICluster>>("CreateClusterAsync", Times.Once(), clusterOptions);
        }

        #endregion

        #region Dispose

        [Fact]
        public async Task Dispose_DisposesCluster()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptions<ClusterOptions>>();
            options.SetupGet(m => m.Value).Returns(clusterOptions);

            var loggerFactory = new Mock<ILoggerFactory>().Object;

            var cluster = new Mock<ICluster>();
            cluster.Setup(m => m.Dispose());

            var provider = new Mock<ClusterProvider>(options.Object, loggerFactory)
            {
                CallBase = true
            };
            provider.Protected()
                .Setup<Task<ICluster>>("CreateClusterAsync", clusterOptions)
                .Returns(Task.FromResult(cluster.Object));

            await provider.Object.GetClusterAsync();

            // Act

            provider.Object.Dispose();

            // Assert

            cluster.Verify(m => m.Dispose(), Times.AtLeastOnce);
        }

        #endregion

        #region DisposeAsync

        [Fact]
        public async Task DisposeAsync_DisposesCluster()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptions<ClusterOptions>>();
            options.SetupGet(m => m.Value).Returns(clusterOptions);

            var loggerFactory = new Mock<ILoggerFactory>().Object;

            var cluster = new Mock<ICluster>();
            cluster.Setup(m => m.Dispose());

            var provider = new Mock<ClusterProvider>(options.Object, loggerFactory)
            {
                CallBase = true
            };
            provider.Protected()
                .Setup<Task<ICluster>>("CreateClusterAsync", clusterOptions)
                .Returns(Task.FromResult(cluster.Object));

            await provider.Object.GetClusterAsync();

            // Act

            await provider.Object.DisposeAsync();

            // Assert

            cluster.Verify(m => m.DisposeAsync(), Times.AtLeastOnce);
        }

        #endregion
    }
}
