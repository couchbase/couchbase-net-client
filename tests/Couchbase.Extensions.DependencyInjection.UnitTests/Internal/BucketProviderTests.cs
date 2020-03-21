using System;
using System.Threading.Tasks;
using Couchbase.Extensions.DependencyInjection.Internal;
using Moq;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests.Internal
{
    public class BucketProviderTests
    {

        #region ctor

        [Fact]
        public void ctor_NoClusterProvider_Exception()
        {
            // Act/Assert

            var ex = Assert.Throws<ArgumentNullException>(() => new BucketProvider(null));

            Assert.Equal("clusterProvider", ex.ParamName);
        }

        #endregion

        #region GetBucketAsync

        [Fact]
        public async Task GetBucketAsync_Disposed_Exception()
        {
            // Arrange

            var clusterProvider = new Mock<IClusterProvider>();

            var provider = new BucketProvider(clusterProvider.Object);
            provider.Dispose();

            // Act/Assert

            var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.GetBucketAsync("bucket1").AsTask());

            Assert.Equal(nameof(BucketProvider), ex.ObjectName);
        }

        [Fact]
        public async Task GetBucketAsync_NullBucketName_Exception()
        {
            // Arrange

            var clusterProvider = new Mock<IClusterProvider>();

            var provider = new BucketProvider(clusterProvider.Object);

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

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider
                .Setup(m => m.GetClusterAsync())
                .ReturnsAsync(cluster.Object);

            var provider = new BucketProvider(clusterProvider.Object);

            // Act

            var result = await provider.GetBucketAsync("bucket1");

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

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider
                .Setup(m => m.GetClusterAsync())
                .ReturnsAsync(cluster.Object);

            var provider = new BucketProvider(clusterProvider.Object);

            // Act

            await provider.GetBucketAsync("bucket1");
            var result = await provider.GetBucketAsync("bucket1");

            // Assert

            Assert.Equal(bucket1.Object, result);
            cluster.Verify(m => m.BucketAsync("bucket1"), Times.Once);
        }

        #endregion

        #region Dispose

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

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider
                .Setup(m => m.GetClusterAsync())
                .ReturnsAsync(cluster.Object);

            var provider = new BucketProvider(clusterProvider.Object);
            await provider.GetBucketAsync("bucket1");
            await provider.GetBucketAsync("bucket2");

            // Act

            provider.Dispose();

            // Assert

            bucket1.Verify(m => m.Dispose(), Times.AtLeastOnce);
            bucket2.Verify(m => m.Dispose(), Times.AtLeastOnce);
        }

        #endregion

        #region Dispose

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

            var clusterProvider = new Mock<IClusterProvider>();
            clusterProvider
                .Setup(m => m.GetClusterAsync())
                .ReturnsAsync(cluster.Object);

            var provider = new BucketProvider(clusterProvider.Object);
            await provider.GetBucketAsync("bucket1");
            await provider.GetBucketAsync("bucket2");

            // Act

            await provider.DisposeAsync();

            // Assert

            bucket1.Verify(m => m.Dispose(), Times.AtLeastOnce);
            bucket2.Verify(m => m.Dispose(), Times.AtLeastOnce);
        }

        #endregion
    }
}
