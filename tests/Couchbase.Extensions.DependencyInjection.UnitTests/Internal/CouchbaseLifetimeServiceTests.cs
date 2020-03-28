using System;
using System.Threading.Tasks;
using Couchbase.Extensions.DependencyInjection.Internal;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests.Internal
{
    public class CouchbaseLifetimeServiceTests
    {

        #region ctor

        [Fact]
        public void ctor_NullProvider_Exception()
        {
            // Act/Assert

            var ex = Assert.Throws<ArgumentNullException>(() => new CouchbaseLifetimeService(null));

            Assert.Equal("serviceProvider", ex.ParamName);
        }

        #endregion

        #region Close

        [Fact]
        public void Close_CouchbaseNotRegistered_NoException()
        {
            // Arrange

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act/Assert

            lifetimeService.Close();
        }

        [Fact]
        public void Close_CouchbaseRegistered_DisposesBucketProvider()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();
            bucketProvider.Setup(m => m.Dispose());

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act

            lifetimeService.Close();

            // Assert

            bucketProvider.Verify(m => m.Dispose(), Times.Once);
        }

        [Fact]
        public void Close_CouchbaseRegistered_DisposesClusterProvider()
        {
            // Arrange

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider.Setup(m => m.Dispose());

            var services = new ServiceCollection();
            services.AddSingleton(clusterProvider.Object);

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act

            lifetimeService.Close();

            // Assert

            clusterProvider.Verify(m => m.Dispose(), Times.Once);
        }

        [Fact]
        public void Close_CouchbaseRegistered_DisposesBucketsBeforeCluster()
        {
            // Arrange

            var bucketDisposed = false;

            var bucketProvider = new Mock<IBucketProvider>();
            bucketProvider
                .Setup(m => m.Dispose())
                .Callback(() =>
                {
                    bucketDisposed = true;
                });

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider
                .Setup(m => m.Dispose())
                .Callback(() =>
                {
                    if (!bucketDisposed)
                    {
                        throw new InvalidOperationException("Bucket was not disposed before cluster");
                    }
                });

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddSingleton(clusterProvider.Object);

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act

            lifetimeService.Close();

            // Assert

            bucketProvider.Verify(m => m.Dispose(), Times.Once);
            clusterProvider.Verify(m => m.Dispose(), Times.Once);
        }

        #endregion

        #region CloseAsync

        [Fact]
        public async Task CloseAsync_CouchbaseNotRegistered_NoException()
        {
            // Arrange

            var serviceProvider = new ServiceCollection().BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act/Assert

            await lifetimeService.CloseAsync();
        }

        [Fact]
        public async Task CloseAsync_CouchbaseRegistered_DisposesBucketProvider()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();
            bucketProvider.Setup(m => m.Dispose());

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act

            await lifetimeService.CloseAsync();

            // Assert

            bucketProvider.Verify(m => m.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task CloseAsync_CouchbaseRegistered_DisposesClusterProvider()
        {
            // Arrange

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider.Setup(m => m.Dispose());

            var services = new ServiceCollection();
            services.AddSingleton(clusterProvider.Object);

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act

            await lifetimeService.CloseAsync();

            // Assert

            clusterProvider.Verify(m => m.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task CloseAsync_CouchbaseRegistered_DisposesBucketsBeforeCluster()
        {
            // Arrange

            var bucketDisposed = false;

            var bucketProvider = new Mock<IBucketProvider>();
            bucketProvider
                .Setup(m => m.Dispose())
                .Callback(() =>
                {
                    bucketDisposed = true;
                });

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider
                .Setup(m => m.Dispose())
                .Callback(() =>
                {
                    if (!bucketDisposed)
                    {
                        throw new InvalidOperationException("Bucket was not disposed before cluster");
                    }
                });

            var services = new ServiceCollection();
            services.AddSingleton(bucketProvider.Object);
            services.AddSingleton(clusterProvider.Object);

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = new CouchbaseLifetimeService(serviceProvider);

            // Act

            await lifetimeService.CloseAsync();

            // Assert

            bucketProvider.Verify(m => m.DisposeAsync(), Times.Once);
            clusterProvider.Verify(m => m.DisposeAsync(), Times.Once);
        }

        #endregion
    }
}
