using System;
using System.Linq;
using Couchbase.Core.DI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.DI
{
    public class SingletonGenericServiceFactoryTests
    {
        #region CreateService

        [Fact]
        public void CreateService_NotInitialized_InvalidOperationException()
        {
            // Arrange

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(m => m.GetService(typeof(ILoggerFactory)))
                .Returns(new NullLoggerFactory());

            var factory = new SingletonGenericServiceFactory(typeof(Logger<>));

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => factory.CreateService(typeof(ILogger<SingletonGenericServiceFactoryTests>)));
        }

        [Fact]
        public void CreateService_Generic_Constructs()
        {
            // Arrange

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(m => m.GetService(typeof(ILoggerFactory)))
                .Returns(new NullLoggerFactory());

            var factory = new SingletonGenericServiceFactory(typeof(Logger<>));
            factory.Initialize(serviceProvider.Object);

            // Act

            var result = factory.CreateService(typeof(ILogger<SingletonGenericServiceFactoryTests>));

            // Assert

            Assert.NotNull(result);
            Assert.IsAssignableFrom<Logger<SingletonGenericServiceFactoryTests>>(result);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        public void CreateService_MultipleTimes_ConstructsOnce(int times)
        {
            // Arrange

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(m => m.GetService(typeof(ILoggerFactory)))
                .Returns(new NullLoggerFactory());

            var factory = new SingletonGenericServiceFactory(typeof(Logger<>));
            factory.Initialize(serviceProvider.Object);

            // Act

            var result = Enumerable.Range(1, times).Select(
                _ => factory.CreateService(typeof(ILogger<SingletonGenericServiceFactoryTests>)))
                .ToList();

            // Assert

            var first = result[0];
            Assert.All(result, p => Assert.Equal(first, p));
        }

        #endregion
    }
}
