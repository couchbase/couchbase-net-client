using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Couchbase.Core.DI;
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

        #endregion
    }
}
