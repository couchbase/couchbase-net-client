using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Bootstrapping;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.UnitTests.Core
{
    public class BootstrapperTests
    {
         [Fact]
         public async Task When_Cannot_Bootstrap_Repeat()
         {
             var callCount = 0;
             var calledTwiceTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
             var mockSubject = new Mock<IBootstrappable>();
             mockSubject.Setup(x => x.BootStrapAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Callback(() =>
                 {
                     if (++callCount >= 2)
                         calledTwiceTcs.TrySetResult(true);
                 });
             mockSubject.Setup(x => x.DeferredExceptions).Returns(new List<Exception>());

             using var tcs = new CancellationTokenSource();
             tcs.CancelAfter(TimeSpan.FromSeconds(10));

             var bootStrapper = new Bootstrapper(tcs, new Mock<ILogger<Bootstrapper>>().Object)
             {
                 SleepDuration = TimeSpan.FromMilliseconds(10)
             };

             bootStrapper.Start(mockSubject.Object);

             // Wait for bootstrap to be called at least twice (TCS signals when threshold reached)
             var completedTask = await Task.WhenAny(calledTwiceTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

             Assert.True(completedTask == calledTwiceTcs.Task, "BootStrapAsync should have been called at least twice");
             mockSubject.Verify(x => x.BootStrapAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
         }

        [Fact]
        public void When_Success_Bootstrap_Do_Not_Repeat()
        {
            var mockSubject = new Mock<IBootstrappable>();
            mockSubject.Setup(x => x.BootStrapAsync()).Returns(Task.CompletedTask);
            mockSubject.Setup(x => x.IsBootstrapped).Returns(true);

            using var tcs = new CancellationTokenSource();
            tcs.CancelAfter(1000);

            var bootStrapper = new Bootstrapper(new Mock<ILogger<Bootstrapper>>().Object)
            {
                SleepDuration = TimeSpan.FromMilliseconds(100)
            };
            bootStrapper.Start(mockSubject.Object);

            mockSubject.Verify(x => x.BootStrapAsync(), Times.Exactly(0));
        }

         [Theory()]
         [InlineData(true)]
         [InlineData(false)]
          public async Task When_Success_IsBootstrapped_Is_True(bool failed)
          {
              var subject = new FakeBootstrappable(failed);
              using var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(10));
              var bootStrapper = new Bootstrapper(tcs, new Mock<ILogger<Bootstrapper>>().Object)
              {
                  SleepDuration = TimeSpan.FromMilliseconds(50)
              };

              if (failed)
              {
                  subject.DeferredExceptions.Add(new Exception());
              }

              bootStrapper.Start(subject);

              if (failed)
              {
                  // For failure case, wait for bootstrap to be attempted and verify state
                  await AsyncTestHelper.WaitForConditionAsync(
                      () => subject.BootstrapAttempted,
                      timeout: TimeSpan.FromSeconds(5));

                  Assert.False(subject.IsBootstrapped);
                  Assert.True(subject.DeferredExceptions.Any());
              }
              else
              {
                  // For success case, poll until bootstrapped (instead of fixed delay)
                  var bootstrapped = await AsyncTestHelper.WaitForConditionAsync(
                      () => subject.IsBootstrapped,
                      timeout: TimeSpan.FromSeconds(5));

                  Assert.True(bootstrapped, "Expected IsBootstrapped to be true after successful bootstrap");
              }
          }
    }

    public class FakeBootstrappable : IBootstrappable
    {
        private bool _hasFailed;

        public FakeBootstrappable(bool hasFailed)
        {
            _hasFailed = hasFailed;
        }

        public bool BootstrapAttempted { get; private set; }

        Task IBootstrappable.BootStrapAsync(CancellationToken cancellationToken)
        {
            BootstrapAttempted = true;
            if (_hasFailed)
            {
                throw new Exception("Bootstrapping has failed.");
            }
            return Task.CompletedTask;
        }

        public bool IsBootstrapped => !DeferredExceptions.Any();
        public List<Exception> DeferredExceptions { get; } = new List<Exception>();
    }
}
