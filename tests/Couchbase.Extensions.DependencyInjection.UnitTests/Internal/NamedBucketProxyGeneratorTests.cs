using System;
using Couchbase.Extensions.DependencyInjection.Internal;
using Moq;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests.Internal
{
    public class NamedBucketProxyGeneratorTests
    {
        #region GetProxy

        [Fact]
        public void GetProxyFactory_GoodInterface_ReturnsProxy()
        {
            //  Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var generator = new NamedBucketProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), "test");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
        }

        [Fact]
        public void GetProxyFactory_TwoInterfaces_ReturnsTwoProxies()
        {
            //  Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var generator = new NamedBucketProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), "test");
            var proxyType2 = generator.GetProxy(typeof(ITestBucketProvider2), "test");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.NotEqual(proxy.GetType(), proxy2.GetType());
        }

        [Fact]
        public void GetProxyFactory_TwoNames_ReturnsTwoProxies()
        {
            //  Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var generator = new NamedBucketProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), "test");
            var proxyType2 = generator.GetProxy(typeof(ITestBucketProvider), "test2");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.NotEqual(proxy.GetType(), proxy2.GetType());
        }

        [Fact]
        public void GetProxyFactory_TwiceWithSameInterfaceAndName_ReturnsSameProxyType()
        {
            //  Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var generator = new NamedBucketProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), "test");
            var proxyType2 = generator.GetProxy(typeof(ITestBucketProvider), "test");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.Equal(proxy.GetType(), proxy2.GetType());
        }

        #endregion

        #region Helpers

        public interface ITestBucketProvider : INamedBucketProvider
        {
        }

        public interface ITestBucketProvider2 : INamedBucketProvider
        {
        }

        #endregion
    }
}
