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

            var ex = Assert.Throws<ArgumentNullException>(() => new CouchbaseLifetimeService(null!));

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
        public void Close_UnkeyedAndKeyed_DisposesKeyedClusterProvider()
        {
            // Arrange

            var clusterProvider1 = new Mock<IClusterProvider>();
            clusterProvider1.Setup(m => m.Dispose());

            var clusterProvider2 = new Mock<IClusterProvider>();
            clusterProvider2.Setup(m => m.Dispose());

            var services = new ServiceCollection();
            services.AddSingleton(clusterProvider1.Object);
            services.AddKeyedSingleton("foo", clusterProvider2.Object);
            services.AddSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>();
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("foo");

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = serviceProvider.GetRequiredKeyedService<ICouchbaseLifetimeService>("foo");

            // Act

            lifetimeService.Close();

            // Assert

            clusterProvider1.Verify(m => m.Dispose(), Times.Never);
            clusterProvider2.Verify(m => m.Dispose(), Times.Once);
        }

        [Fact]
        public void Close_UnkeyedAndKeyed_DisposesUnkeyedClusterProvider()
        {
            // Arrange

            var clusterProvider1 = new Mock<IClusterProvider>();
            clusterProvider1.Setup(m => m.Dispose());

            var clusterProvider2 = new Mock<IClusterProvider>();
            clusterProvider2.Setup(m => m.Dispose());

            var services = new ServiceCollection();
            services.AddSingleton(clusterProvider1.Object);
            services.AddKeyedSingleton("foo", clusterProvider2.Object);
            services.AddSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>();
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("foo");

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = serviceProvider.GetRequiredService<ICouchbaseLifetimeService>();

            // Act

            lifetimeService.Close();

            // Assert

            clusterProvider1.Verify(m => m.Dispose(), Times.Once);
            clusterProvider2.Verify(m => m.Dispose(), Times.Never);
        }

        [Fact]
        public void Close_TwoKeyed_DisposesCorrectClusterProvider()
        {
            // Arrange

            var clusterProvider1 = new Mock<IClusterProvider>();
            clusterProvider1.Setup(m => m.Dispose());

            var clusterProvider2 = new Mock<IClusterProvider>();
            clusterProvider2.Setup(m => m.Dispose());

            var services = new ServiceCollection();
            services.AddKeyedSingleton("foo", clusterProvider1.Object);
            services.AddKeyedSingleton("bar", clusterProvider2.Object);
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("foo");
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("bar");

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = serviceProvider.GetRequiredKeyedService<ICouchbaseLifetimeService>("foo");

            // Act

            lifetimeService.Close();

            // Assert

            clusterProvider1.Verify(m => m.Dispose(), Times.Once);
            clusterProvider2.Verify(m => m.Dispose(), Times.Never);
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
        public async Task CloseAsync_UnkeyedAndKeyed_DisposesKeyedClusterProvider()
        {
            // Arrange

            var clusterProvider1 = new Mock<IClusterProvider>();
            clusterProvider1.Setup(m => m.DisposeAsync());

            var clusterProvider2 = new Mock<IClusterProvider>();
            clusterProvider2.Setup(m => m.DisposeAsync());

            var services = new ServiceCollection();
            services.AddSingleton(clusterProvider1.Object);
            services.AddKeyedSingleton("foo", clusterProvider2.Object);
            services.AddSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>();
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("foo");

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = serviceProvider.GetRequiredKeyedService<ICouchbaseLifetimeService>("foo");

            // Act

            await lifetimeService.CloseAsync();

            // Assert

            clusterProvider1.Verify(m => m.DisposeAsync(), Times.Never);
            clusterProvider2.Verify(m => m.DisposeAsync(), Times.Once);
        }

        [Fact]
        public async Task CloseAsync_UnkeyedAndKeyed_DisposesUnkeyedClusterProvider()
        {
            // Arrange

            var clusterProvider1 = new Mock<IClusterProvider>();
            clusterProvider1.Setup(m => m.DisposeAsync());

            var clusterProvider2 = new Mock<IClusterProvider>();
            clusterProvider2.Setup(m => m.DisposeAsync());

            var services = new ServiceCollection();
            services.AddSingleton(clusterProvider1.Object);
            services.AddKeyedSingleton("foo", clusterProvider2.Object);
            services.AddSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>();
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("foo");

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = serviceProvider.GetRequiredService<ICouchbaseLifetimeService>();

            // Act

            await lifetimeService.CloseAsync();

            // Assert

            clusterProvider1.Verify(m => m.DisposeAsync(), Times.Once);
            clusterProvider2.Verify(m => m.DisposeAsync(), Times.Never);
        }

        [Fact]
        public async Task CloseAsync_TwoKeyed_DisposesCorrectClusterProvider()
        {
            // Arrange

            var clusterProvider1 = new Mock<IClusterProvider>();
            clusterProvider1.Setup(m => m.DisposeAsync());

            var clusterProvider2 = new Mock<IClusterProvider>();
            clusterProvider2.Setup(m => m.DisposeAsync());

            var services = new ServiceCollection();
            services.AddKeyedSingleton("foo", clusterProvider1.Object);
            services.AddKeyedSingleton("bar", clusterProvider2.Object);
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("foo");
            services.AddKeyedSingleton<ICouchbaseLifetimeService, CouchbaseLifetimeService>("bar");

            var serviceProvider = services.BuildServiceProvider();

            var lifetimeService = serviceProvider.GetRequiredKeyedService<ICouchbaseLifetimeService>("foo");

            // Act

            await lifetimeService.CloseAsync();

            // Assert

            clusterProvider1.Verify(m => m.DisposeAsync(), Times.Once);
            clusterProvider2.Verify(m => m.DisposeAsync(), Times.Never);
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
