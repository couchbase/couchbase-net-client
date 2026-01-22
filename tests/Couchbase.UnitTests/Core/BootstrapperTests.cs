using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Bootstrapping;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.UnitTests.Core
{
    public class BootstrapperTests
    {
         [Fact(Skip = "Only fails on Jenkins.")]
         public async Task When_Cannot_Bootstrap_Repeat()
         {
             var mockSubject = new Mock<IBootstrappable>();
             mockSubject.Setup(x => x.BootStrapAsync()).Returns(Task.CompletedTask);
             mockSubject.Setup(x => x.DeferredExceptions).Returns(new List<Exception>());

             using var tcs = new CancellationTokenSource();
             tcs.CancelAfter(1000);

             var bootStrapper = new Bootstrapper(tcs, new Mock<ILogger<Bootstrapper>>().Object)
             {
                 SleepDuration = TimeSpan.FromMilliseconds(10)
             };

             bootStrapper.Start(mockSubject.Object);

             await Task.Delay(1200);
             mockSubject.Verify(x => x.BootStrapAsync(), Times.AtLeast(2));
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
              using var tcs = new CancellationTokenSource(100);
              var bootStrapper = new Bootstrapper(tcs, new Mock<ILogger<Bootstrapper>>().Object)
              {
                  SleepDuration = TimeSpan.FromMilliseconds(50)
              };

              if (failed)
              {
                  subject.DeferredExceptions.Add(new Exception());
              }

              bootStrapper.Start(subject);

              await Task.Delay(400);
              if (failed)
              {
                  Assert.False(subject.IsBootstrapped);
                  Assert.True(subject.DeferredExceptions.Any());
              }
              else
              {
                  Assert.True(subject.IsBootstrapped);
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

        Task IBootstrappable.BootStrapAsync()
        {
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
