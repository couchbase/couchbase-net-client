using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.DI;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.DI
{
    public class BootstrapperFactoryTests
    {
        [Fact]
        public async Task Create_BootstrapperFactory()
        {
            //arrange
            var clock = new SteppableClock();
            var bootstrapperFactory = new BootstrapperFactory(new Mock<ILogger<Bootstrapper>>().Object, clock);
            var sleepDuration = TimeSpan.FromMinutes(1);

            var subject = new Mock<IBootstrappable>();
            subject.Setup(x => x.BootStrapAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            subject.Setup(x => x.DeferredExceptions).Returns(new List<Exception>());

            //act
            using var bootstrapper = bootstrapperFactory.Create(sleepDuration);
            bootstrapper.Start(subject.Object);

            //assert
            Assert.Equal(sleepDuration, bootstrapper.SleepDuration);

            // The bootstrapper it built polls on the clock the factory was given, waiting the duration
            // it was created with — so nothing here waits on the real clock, and the loop is only ever
            // driven forwards by this test.
            await clock.WaitForSleepAsync(sleepDuration);
            subject.Verify(x => x.BootStrapAsync(It.IsAny<CancellationToken>()), Times.Once);

            clock.Advance(sleepDuration);
            await clock.WaitForSleepAsync(sleepDuration);
            subject.Verify(x => x.BootStrapAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public void BootstrapperFactory_Is_Resolvable()
        {
            // The factory is built by reflection over its longest public constructor, so a dependency it
            // takes but the container does not register only fails when a bucket is opened.
            var serviceProvider = new ClusterOptions().BuildServiceProvider();

            var bootstrapperFactory = serviceProvider.GetRequiredService<IBootstrapperFactory>();

            Assert.NotNull(bootstrapperFactory.Create(TimeSpan.FromMinutes(1)));

            // And it is the system clock which is handed out, so the bootstrapper's poll interval is
            // still waited by Task.Delay: on the system provider that is the code path TimeProvider
            // delegates to, which is what makes injecting one a change of seam and not of behaviour.
            Assert.Same(TimeProvider.System, serviceProvider.GetRequiredService<TimeProvider>());
        }
    }
}
