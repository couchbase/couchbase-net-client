using System;
using System.Linq;
using Couchbase.Core.DI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.DI
{
    public class SingletonServiceFactoryTests
    {
        #region CreateService

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void CreateService_CalledXTimes_ReturnsSingleton(int times)
        {
            // Arrange

            var obj = new object();

            var factory = new SingletonServiceFactory(obj);

            // Act

            var result = Enumerable.Range(1, times).Select(p => factory.CreateService(typeof(object)));

            // Assert

            Assert.All(result, p => Assert.Equal(obj, p));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void CreateService_LambdaCalledXTimes_ReturnsSingleton(int times)
        {
            // Arrange

            var serviceProvider = new Mock<IServiceProvider>();

            var factory = new SingletonServiceFactory(localServiceProvider =>
            {
                Assert.Equal(serviceProvider.Object, localServiceProvider);

                return new object();
            });
            factory.Initialize(serviceProvider.Object);

            // Act

            var result = Enumerable.Range(1, times).Select(p => factory.CreateService(typeof(object))).ToList();

            // Assert

            var obj = result.First();
            Assert.All(result, p => Assert.Equal(obj, p));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void CreateService_Type_ReturnsSingleton(int times)
        {
            // Arrange

            var serviceProvider = new Mock<IServiceProvider>();

            var factory = new SingletonServiceFactory(typeof(NoParametersType));
            factory.Initialize(serviceProvider.Object);

            // Act

            var result = Enumerable.Range(1, times).Select(p => factory.CreateService(typeof(NoParametersType))).ToList();

            // Assert

            var obj = result.First();
            Assert.All(result, p => Assert.Equal(obj, p));
        }

        [Fact]
        public void CreateService_Type_PopulatesConstructorParams()
        {
            // Arrange

            var logger = new Mock<ILogger>().Object;

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(m => m.GetService(typeof(ILogger)))
                .Returns(logger);

            var factory = new SingletonServiceFactory(typeof(WithParametersType));
            factory.Initialize(serviceProvider.Object);

            // Act

            var result =  factory.CreateService(typeof(WithParametersType));

            // Assert

            Assert.NotNull(result);
            var typedResult = Assert.IsAssignableFrom<WithParametersType>(result);
            Assert.Equal(logger, typedResult.Logger);
        }

        #endregion

        #region Helpers

        private class NoParametersType
        {
        }

        private class WithParametersType : NoParametersType
        {
            public ILogger Logger { get; }

            public WithParametersType() { }

            public WithParametersType(ILogger logger)
            {
                Logger = logger;
            }
        }

        #endregion
    }
}
