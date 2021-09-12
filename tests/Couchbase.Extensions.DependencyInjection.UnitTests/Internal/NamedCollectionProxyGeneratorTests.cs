using System;
using Couchbase.Extensions.DependencyInjection.Internal;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Couchbase.Extensions.DependencyInjection.UnitTests.Internal
{
    public class NamedCollectionProxyGeneratorTests
    {
        #region GetProxy

        [Fact]
        public void GetProxyFactory_GoodInterface_ReturnsProxy()
        {
            //  Arrange

            var bucketProvider = new Mock<ITestBucketProvider>();

            var generator = new NamedCollectionProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), "scope", "collection");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
        }

        [Fact]
        public void GetProxyFactory_TwoInterfaces_ReturnsTwoProxies()
        {
            //  Arrange

            var bucketProvider = new Mock<ITestBucketProvider>();

            var generator = new NamedCollectionProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), "scope", "collection");
            var proxyType2 = generator.GetProxy(typeof(ITestCollectionProvider2), typeof(ITestBucketProvider), "scope", "collection");

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

            var bucketProvider = new Mock<ITestBucketProvider>();

            var generator = new NamedCollectionProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), "scope", "collection");
            var proxyType2 = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), "scope2", "collection");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.NotEqual(proxy.GetType(), proxy2.GetType());
        }

        [Fact]
        public void GetProxyFactory_TwiceWithSameBucketInterfaceAndName_ReturnsSameProxyType()
        {
            //  Arrange

            var bucketProvider = new Mock<ITestBucketProvider>();

            var generator = new NamedCollectionProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), "scope", "collection");
            var proxyType2 = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), "scope", "collection");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.Equal(proxy.GetType(), proxy2.GetType());
        }

        #endregion

        #region Helpers

        public interface ITestCollectionProvider : INamedCollectionProvider
        {
        }

        public interface ITestCollectionProvider2 : INamedCollectionProvider
        {
        }

        public interface ITestBucketProvider : INamedBucketProvider
        {
        }

        #endregion
    }
}
