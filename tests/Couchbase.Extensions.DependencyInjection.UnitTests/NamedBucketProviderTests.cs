using System;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests
{
    public class NamedBucketProviderTests
    {
        #region ctor

        [Fact]
        public void ctor_NullBucketProvider_Exception()
        {
            // Act/Assert

            var ex = Assert.Throws<ArgumentNullException>(() => new LocalNamedBucketProvider(null, "bucket"));

            Assert.Equal("bucketProvider", ex.ParamName);
        }

        [Fact]
        public void ctor_NullBucketName_Exception()
        {
            // Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            // Act/Assert

            var ex =
                Assert.Throws<ArgumentNullException>(
                    () => new LocalNamedBucketProvider(bucketProvider.Object, null));

            Assert.Equal("bucketName", ex.ParamName);
        }

        #endregion

        #region GetBucket

        [Fact]
        public async Task GetBucket_UsesParametersToGetBucketFromProvider()
        {
            // Arrange

            var bucket = new Mock<IBucket>();

            var bucketProvider = new Mock<IBucketProvider>();
            bucketProvider
                .Setup(m => m.GetBucketAsync("bucket"))
                .ReturnsAsync(bucket.Object);

            var namedBucketProvider = new LocalNamedBucketProvider(bucketProvider.Object, "bucket");

            // Act

            var result = await namedBucketProvider.GetBucketAsync();

            // Assert

            Assert.Equal(bucket.Object, result);
        }

        #endregion

        #region Helpers

        private class LocalNamedBucketProvider : NamedBucketProvider
        {
            public LocalNamedBucketProvider(IBucketProvider bucketProvider, string bucketName)
                : base(bucketProvider, bucketName)
            {
            }
        }

        #endregion
    }
}
