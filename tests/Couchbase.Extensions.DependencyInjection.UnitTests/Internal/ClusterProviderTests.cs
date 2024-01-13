using System;
using System.Threading.Tasks;
using Couchbase.Extensions.DependencyInjection.Internal;
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

            var ex = Assert.Throws<ArgumentNullException>(() => new ClusterProvider(null!, null));

            Assert.Equal("options", ex.ParamName);
        }

        #endregion

        #region GetCluster

        [Fact]
        public async Task GetClusterAsync_Disposed_Exception()
        {
            // Arrange

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();

            var provider = new ClusterProvider(options.Object, null);
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

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var cluster = new Mock<ICluster>();

            var provider = new Mock<ClusterProvider>(options.Object, null)
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

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var cluster = new Mock<ICluster>();

            var provider = new Mock<ClusterProvider>(options.Object, null)
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

        [Fact]
        public async Task GetCluster_WithNullServiceKey_UsesDefaultOptions()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(""))
                .Returns(clusterOptions);

            var cluster = new Mock<ICluster>();

            var provider = new Mock<ClusterProvider>(options.Object, null)
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
            provider.Protected()
                .Verify<Task<ICluster>>("CreateClusterAsync", Times.Once(), clusterOptions);
        }

        [Fact]
        public async Task GetCluster_WithServiceKey_UsesKeyedOptions()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            const string serviceKey = "foo";

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(serviceKey))
                .Returns(clusterOptions);

            var cluster = new Mock<ICluster>();

            var provider = new Mock<ClusterProvider>(options.Object, serviceKey)
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
            provider.Protected()
                .Verify<Task<ICluster>>("CreateClusterAsync", Times.Once(), clusterOptions);
        }

        #endregion

        #region GetBucketAsync

        [Fact]
        public async Task GetBucketAsync_Disposed_Exception()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var provider = new Mock<ClusterProvider>(options.Object).Object;
            provider.Dispose();

            // Act/Assert

            var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.GetBucketAsync("bucket1").AsTask());

            Assert.Equal(nameof(ClusterProvider), ex.ObjectName);
        }

        [Fact]
        public async Task GetBucketAsync_NullBucketName_Exception()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var provider = new ClusterProvider(options.Object);

            // Act/Assert

            var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => provider.GetBucketAsync(null).AsTask());

            Assert.Equal("bucketName", ex.ParamName);
        }

        [Fact]
        public async Task GetBucket_FirstTime_OpensBucket()
        {
            // Arrange

            var bucket1 = new Mock<IBucket>();

            var cluster = new Mock<ICluster>();
            cluster
                .Setup(m => m.BucketAsync("bucket1"))
                .ReturnsAsync(bucket1.Object);

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>() { CallBase = true };
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var provider = new Mock<ClusterProvider>(options.Object);
            provider
                .Setup(m => m.GetClusterAsync())
                .ReturnsAsync(cluster.Object);

            // Act

            var result = await provider.Object.GetBucketAsync("bucket1");

            // Assert

            Assert.Equal(bucket1.Object, result);
        }

        [Fact]
        public async Task GetBucket_MultipleTimes_ReturnsFirstOpenBucket()
        {
            // Arrange

            var bucket1 = new Mock<IBucket>();

            var cluster = new Mock<ICluster>();
            cluster
                .Setup(m => m.BucketAsync("bucket1"))
                .ReturnsAsync(bucket1.Object);

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var provider = new Mock<ClusterProvider>(options.Object) { CallBase = true };
            provider
                .Setup(m => m.GetClusterAsync())
                .ReturnsAsync(cluster.Object);

            // Act

            await provider.Object.GetBucketAsync("bucket1");
            var result = await provider.Object.GetBucketAsync("bucket1");

            // Assert

            Assert.Equal(bucket1.Object, result);
            cluster.Verify(m => m.BucketAsync("bucket1"), Times.Once);
        }

        [Fact]
        public async Task GetBucket_FirstTimeFails_OpensAgain()
        {
            // Arrange

            var bucket1 = new Mock<IBucket>();

            var cluster = new Mock<ICluster>();
            cluster
                .SetupSequence(m => m.BucketAsync("bucket1"))
                .Returns(() => new ValueTask<IBucket>(Task.Run(async Task<IBucket> () =>
                {
                    await Task.Delay(10);

                    throw new InvalidOperationException("test");
                })))
                .ReturnsAsync(bucket1.Object);

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var provider = new Mock<ClusterProvider>(options.Object) { CallBase = true };
            provider
                .Setup(m => m.GetClusterAsync())
                .ReturnsAsync(cluster.Object);

            var firstEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return provider.Object.GetBucketAsync("bucket1").AsTask();
            });
            Assert.Equal("test", firstEx.Message);

            // Act

            var result = await provider.Object.GetBucketAsync("bucket1");

            // Assert

            Assert.Equal(bucket1.Object, result);
            cluster.Verify(m => m.BucketAsync("bucket1"), Times.Exactly(2));
        }

        #endregion

        #region Dispose

        [Fact]
        public async Task Dispose_DisposesCluster()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var cluster = new Mock<ICluster>();
            cluster.Setup(m => m.Dispose());

            var provider = new Mock<ClusterProvider>(options.Object, null)
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

        [Fact]
        public async Task Dispose_DisposesBuckets()
        {
            // Arrange

            var bucket1 = new Mock<IBucket>();
            bucket1.Setup(m => m.Dispose());

            var bucket2 = new Mock<IBucket>();
            bucket2.Setup(m => m.Dispose());

            var cluster = new Mock<ICluster>();
            cluster
                .Setup(m => m.BucketAsync("bucket1"))
                .ReturnsAsync(bucket1.Object);
            cluster
                .Setup(m => m.BucketAsync("bucket2"))
                .ReturnsAsync(bucket2.Object);

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var provider = new Mock<ClusterProvider>(options.Object) { CallBase = true };
            provider.Protected()
                .Setup<Task<ICluster>>("CreateClusterAsync", clusterOptions)
                .Returns(Task.FromResult(cluster.Object));

            await provider.Object.GetBucketAsync("bucket1");
            await provider.Object.GetBucketAsync("bucket2");

            // Act

            provider.Object.Dispose();

            // Assert

            bucket1.Verify(m => m.Dispose(), Times.AtLeastOnce);
            bucket2.Verify(m => m.Dispose(), Times.AtLeastOnce);
        }

        #endregion

        #region DisposeAsync

        [Fact]
        public async Task DisposeAsync_DisposesCluster()
        {
            // Arrange

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var cluster = new Mock<ICluster>();
            cluster.Setup(m => m.Dispose());

            var provider = new Mock<ClusterProvider>(options.Object, null)
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

        [Fact]
        public async Task DisposeAsync_DisposesBuckets()
        {
            // Arrange

            var bucket1 = new Mock<IBucket>();
            bucket1.Setup(m => m.Dispose());

            var bucket2 = new Mock<IBucket>();
            bucket2.Setup(m => m.Dispose());

            var cluster = new Mock<ICluster>();
            cluster
                .Setup(m => m.BucketAsync("bucket1"))
                .ReturnsAsync(bucket1.Object);
            cluster
                .Setup(m => m.BucketAsync("bucket2"))
                .ReturnsAsync(bucket2.Object);

            var clusterOptions = new ClusterOptions();

            var options = new Mock<IOptionsMonitor<ClusterOptions>>();
            options
                .Setup(m => m.Get(It.IsAny<string>()))
                .Returns(clusterOptions);

            var provider = new Mock<ClusterProvider>(options.Object) { CallBase = true };
            provider.Protected()
                .Setup<Task<ICluster>>("CreateClusterAsync", clusterOptions)
                .Returns(Task.FromResult(cluster.Object));

            await provider.Object.GetBucketAsync("bucket1");
            await provider.Object.GetBucketAsync("bucket2");

            // Act

            await provider.Object.DisposeAsync();

            // Assert

            bucket1.Verify(m => m.DisposeAsync(), Times.AtLeastOnce);
            bucket2.Verify(m => m.DisposeAsync(), Times.AtLeastOnce);
        }

#endregion
    }
}
