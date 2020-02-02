using System;
using System.Linq;
using Couchbase.Core.DI;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.DI
{
    public class LambdaServiceFactoryTests
    {
        #region CreateService

        [Fact]
        public void CreateService_NotInitialized_InvalidOperationException()
        {
            // Arrange

            var factory = new TransientServiceFactory(_ => null);

            // Act

            Assert.Throws<InvalidOperationException>(() => factory.CreateService(typeof(object)));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void CreateService_MultipleCalls_InvokesLambdaEachTime(int times)
        {
            // Arrange

            var serviceProvider = new Mock<IServiceProvider>();

            var count = 0;
            var factory = new TransientServiceFactory(provider =>
            {
                Assert.Equal(serviceProvider.Object, provider);
                count++;

                return null;
            });

            factory.Initialize(serviceProvider.Object);

            // Act

            foreach (var _ in Enumerable.Range(1, times))
            {
                factory.CreateService(typeof(object));
            }

            // Assert

            Assert.Equal(times, count);
        }

        #endregion
    }
}
