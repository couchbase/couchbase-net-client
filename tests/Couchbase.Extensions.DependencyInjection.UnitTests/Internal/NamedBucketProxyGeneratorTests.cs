using System;
using System.Linq;
using Couchbase.Extensions.DependencyInjection.Internal;
using Microsoft.Extensions.DependencyInjection;
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

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), null, "test");

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

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), null, "test");
            var proxyType2 = generator.GetProxy(typeof(ITestBucketProvider2), null, "test");

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

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), null, "test");
            var proxyType2 = generator.GetProxy(typeof(ITestBucketProvider), null, "test2");

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

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), null, "test");
            var proxyType2 = generator.GetProxy(typeof(ITestBucketProvider), null, "test");

            var proxy = Activator.CreateInstance(proxyType, bucketProvider.Object);
            var proxy2 = Activator.CreateInstance(proxyType2, bucketProvider.Object);

            // Assert

            Assert.NotNull(proxy);
            Assert.NotNull(proxy2);
            Assert.Equal(proxy.GetType(), proxy2.GetType());
        }

        [Fact]
        public void GetProxyFactory_TwiceWithSameInterfaceAndNameButDifferentKey_ReturnsTwoProxies()
        {
            //  Arrange

            var bucketProvider = new Mock<IBucketProvider>();

            var generator = new NamedBucketProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), null, "test");
            var proxyType2 = generator.GetProxy(typeof(ITestBucketProvider), "foo", "test");

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

            var generator = new NamedBucketProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), null, "test");

            // Assert

            var parameter = proxyType.GetConstructors().FirstOrDefault()?.GetParameters()
                .FirstOrDefault(p => p.ParameterType == typeof(IBucketProvider));
            Assert.NotNull(parameter);
            Assert.Empty(parameter.CustomAttributes);
        }

        [Fact]
        public void GetProxyFactory_HasServiceKey_HasAnnotation()
        {
            //  Arrange

            const string serviceKey = "foo";

            var generator = new NamedBucketProxyGenerator(new ProxyModuleBuilder());

            // Act

            var proxyType = generator.GetProxy(typeof(ITestBucketProvider), serviceKey, "test");

            // Assert

            var parameter = proxyType.GetConstructors().FirstOrDefault()?.GetParameters()
                .FirstOrDefault(p => p.ParameterType == typeof(IBucketProvider));
            Assert.NotNull(parameter);

            var annotation = parameter.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(FromKeyedServicesAttribute));
            Assert.NotNull(annotation);

            var argument = annotation.ConstructorArguments.FirstOrDefault().Value;
            Assert.Equal(serviceKey, argument);
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
