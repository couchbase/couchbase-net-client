using System;
using System.Linq;
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

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope", "collection");

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

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope", "collection");
            var proxyType2 = generator.GetProxy(typeof(ITestCollectionProvider2), typeof(ITestBucketProvider), null, "scope", "collection");

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

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope", "collection");
            var proxyType2 = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope2", "collection");

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

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope", "collection");
            var proxyType2 = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope", "collection");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.Equal(proxy.GetType(), proxy2.GetType());
        }

        [Fact]
        public void GetProxyFactory_TwiceWithSameBucketInterfaceAndNameButDifferentKey_ReturnsTwoProxies()
        {
            //  Arrange

            var bucketProvider = new Mock<ITestBucketProvider>();

            var generator = new NamedCollectionProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope", "collection");
            var proxyType2 = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), "foo", "scope", "collection");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.NotEqual(proxy.GetType(), proxy2.GetType());
        }

        [Fact]
        public void GetProxyFactory_NoServiceKey_NoAnnotation()
        {
            //  Arrange

            var generator = new NamedCollectionProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), null, "scope", "collection");

            // Assert

            var parameter = proxyType.GetConstructors().FirstOrDefault()?.GetParameters()
                .FirstOrDefault(p => p.ParameterType == typeof(ITestBucketProvider));
            Assert.NotNull(parameter);
            Assert.Empty(parameter.CustomAttributes);
        }

        [Fact]
        public void GetProxyFactory_HasServiceKey_HasAnnotation()
        {
            //  Arrange

            const string serviceKey = "foo";

            var generator = new NamedCollectionProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestCollectionProvider), typeof(ITestBucketProvider), serviceKey, "scope", "collection");

            // Assert

            var parameter = proxyType.GetConstructors().FirstOrDefault()?.GetParameters()
                .FirstOrDefault(p => p.ParameterType == typeof(ITestBucketProvider));
            Assert.NotNull(parameter);

            var annotation = parameter.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(FromKeyedServicesAttribute));
            Assert.NotNull(annotation);

            var argument = annotation.ConstructorArguments.FirstOrDefault().Value;
            Assert.Equal(serviceKey, argument);
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
