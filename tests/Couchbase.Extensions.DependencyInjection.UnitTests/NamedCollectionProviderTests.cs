using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests
{
    public class NamedCollectionProviderTests
    {
        #region ctor

        [Fact]
        public void ctor_NullBucketProvider_Exception()
        {
            // Act/Assert

            var ex = Assert.Throws<ArgumentNullException>(() => new LocalCollectionBucketProvider(null, "scope", "collection"));

            Assert.Equal("bucketProvider", ex.ParamName);
        }

        [Fact]
        public void ctor_NullScopeName_Exception()
        {
            // Arrange

            var bucketProvider = new Mock<INamedBucketProvider>();

            // Act/Assert

            var ex =
                Assert.Throws<ArgumentNullException>(
                    () => new LocalCollectionBucketProvider(bucketProvider.Object, null, "collection"));

            Assert.Equal("scopeName", ex.ParamName);
        }

        [Fact]
        public void ctor_NullCollectionName_Exception()
        {
            // Arrange

            var bucketProvider = new Mock<INamedBucketProvider>();

            // Act/Assert

            var ex =
                Assert.Throws<ArgumentNullException>(
                    () => new LocalCollectionBucketProvider(bucketProvider.Object, "scope", null));

            Assert.Equal("collectionName", ex.ParamName);
        }

        #endregion

        #region GetCollectionAsync

        [Fact]
        public async Task GetBucket_UsesParametersToGetBucketFromProvider()
        {
            // Arrange

            var collection = new Mock<ICouchbaseCollection>();

            var scope = new Mock<IScope>();
            scope
                .Setup(m => m.Collection("collection"))
                .Returns(collection.Object);

            var bucket = new Mock<IBucket>();
            bucket
                .Setup(m => m.Scope("scope"))
                .Returns(scope.Object);

            var bucketProvider = new Mock<INamedBucketProvider>();
            bucketProvider
                .Setup(m => m.GetBucketAsync())
                .ReturnsAsync(bucket.Object);

            var namedBucketProvider = new LocalCollectionBucketProvider(bucketProvider.Object, "scope", "collection");

            // Act

            var result = await namedBucketProvider.GetCollectionAsync();

            // Assert

            Assert.Equal(collection.Object, result);
        }

        [Fact]
        public async Task GetCollectionAsync_MultipleCalls_ReturnsCachedResult()
        {
            // Arrange

            var collection = new Mock<ICouchbaseCollection>();

            var scope = new Mock<IScope>();
            scope
                .Setup(m => m.Collection("collection"))
                .Returns(collection.Object);

            var bucket = new Mock<IBucket>();
            bucket
                .Setup(m => m.Scope("scope"))
                .Returns(scope.Object);

            var bucketProvider = new Mock<INamedBucketProvider>();
            bucketProvider
                .Setup(m => m.GetBucketAsync())
                .ReturnsAsync(bucket.Object);

            var namedBucketProvider = new LocalCollectionBucketProvider(bucketProvider.Object, "scope", "collection");

            // Act

            var result = await namedBucketProvider.GetCollectionAsync();
            var result2 = await namedBucketProvider.GetCollectionAsync();

            // Assert

            Assert.Equal(result, result2);

            bucketProvider.Verify(
                m => m.GetBucketAsync(),
                Times.Once);
            scope.Verify(
                m => m.Collection(It.IsAny<string>()),
                Times.Once);
        }

        #endregion

        #region Helpers

        private class LocalCollectionBucketProvider : NamedCollectionProvider
        {
            public LocalCollectionBucketProvider(INamedBucketProvider bucketProvider, string scopeName, string collectionName)
                : base(bucketProvider, scopeName, collectionName)
            {
            }
        }

        #endregion
    }
}
