using System;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.DI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.DI
{
    public class BootstrapperFactoryTests
    {
        [Fact]
        public void Create_BootstrapperFactory()
        {
            //arrange
            var bootstrapperFactory = new BootstrapperFactory(new Mock<ILogger<Bootstrapper>>().Object);

            //act
            var bootstrapper = bootstrapperFactory.Create(TimeSpan.FromMinutes(1));

            //assert
            Assert.NotNull(bootstrapper);
        }
    }
}
