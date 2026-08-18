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
             var completedTask = await Task.WhenAny(calledTwiceTcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

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
                      timeout: TimeSpan.FromSeconds(30));

                  Assert.True(bootstrapped, "Expected IsBootstrapped to be true after successful bootstrap");
              }
          }
         [Fact]
         public async Task When_Bootstrap_Defers_Failure_Without_Throwing_Failure_Is_Not_Cleared()
         {
             // Cluster.BootStrapAsync records a bootstrap failure in DeferredExceptions and returns
             // normally rather than throwing, so a normal return must not be reported as success.
             var subject = new DeferringBootstrappable();
             using var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(10));
             var bootStrapper = new Bootstrapper(tcs, new Mock<ILogger<Bootstrapper>>().Object)
             {
                 SleepDuration = TimeSpan.FromMilliseconds(50)
             };

             // Start un-bootstrapped, so the bootstrapper actually makes an attempt.
             subject.DeferredExceptions.Add(new Exception("Earlier failure."));

             bootStrapper.Start(subject);

             // Retrying at all proves the failed attempt was not mistaken for a success. The subject
             // blocks on entry to the second attempt, so observing it means the first attempt has been
             // fully processed and the list cannot change underneath the assertions below.
             var retried = await AsyncTestHelper.WaitForConditionAsync(
                 () => subject.AttemptCount >= 2,
                 timeout: TimeSpan.FromSeconds(5));

             Assert.True(retried, "Expected the bootstrapper to keep retrying after a deferred failure");
             Assert.False(subject.IsBootstrapped);
             Assert.NotEmpty(subject.DeferredExceptions);

             // Stale failures are still discarded, so repeated attempts must not accumulate.
             Assert.Single(subject.DeferredExceptions);

             // Let the blocked attempt finish so the bootstrapper loop unwinds on cancellation.
             subject.Release();
         }

         [Fact]
         public async Task When_Subject_Clears_Its_Own_Failures_This_Attempts_Failure_Survives()
         {
             // CouchbaseBucket clears DeferredExceptions itself before recording a new failure, so the
             // stale entries cannot be discarded by position — that would remove this attempt's failure
             // instead, report success and stop the retry loop.
             var subject = new ClearingBootstrappable();
             using var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(10));
             var bootStrapper = new Bootstrapper(tcs, new Mock<ILogger<Bootstrapper>>().Object)
             {
                 SleepDuration = TimeSpan.FromMilliseconds(50)
             };

             // Start un-bootstrapped, so the bootstrapper actually makes an attempt.
             subject.DeferredExceptions.Add(new Exception("Earlier failure."));

             bootStrapper.Start(subject);

             var retried = await AsyncTestHelper.WaitForConditionAsync(
                 () => subject.AttemptCount >= 2,
                 timeout: TimeSpan.FromSeconds(5));

             Assert.True(retried, "Expected the bootstrapper to keep retrying; the new failure was discarded");
             Assert.False(subject.IsBootstrapped);
             Assert.Single(subject.DeferredExceptions);

             subject.Release();
         }

         [Fact]
         public async Task When_Bootstrap_Throws_Failures_Do_Not_Accumulate()
         {
             // BucketBase.BootStrapAsync throws rather than defers, and the bootstrapper records the
             // exception on every poll. Stale failures must still be discarded or the list grows
             // without bound for as long as the subject stays broken.
             var subject = new ThrowingBootstrappable();
             using var tcs = new CancellationTokenSource(TimeSpan.FromSeconds(10));
             var bootStrapper = new Bootstrapper(tcs, new Mock<ILogger<Bootstrapper>>().Object)
             {
                 SleepDuration = TimeSpan.FromMilliseconds(50)
             };

             // Start un-bootstrapped, so the bootstrapper actually makes an attempt.
             subject.DeferredExceptions.Add(new Exception("Earlier failure."));

             bootStrapper.Start(subject);

             // The subject blocks on entry to the third attempt, so observing it means the first two
             // were processed to completion and the list cannot change under the assertion.
             var retried = await AsyncTestHelper.WaitForConditionAsync(
                 () => subject.AttemptCount >= 3,
                 timeout: TimeSpan.FromSeconds(5));

             Assert.True(retried, "Expected the bootstrapper to keep retrying after a thrown failure");
             Assert.False(subject.IsBootstrapped);

             // Only the most recent attempt's failure is retained, however many attempts have run.
             Assert.Single(subject.DeferredExceptions);

             subject.Release();
         }
    }

    /// <summary>
    /// Records a bootstrap failure the way <see cref="Cluster"/> does — deferred, without throwing.
    /// </summary>
    public class DeferringBootstrappable : IBootstrappable
    {
        private readonly TaskCompletionSource<bool> _released =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _attemptCount;

        public int AttemptCount => Volatile.Read(ref _attemptCount);

        /// <summary>
        /// Unblocks the second and subsequent attempts.
        /// </summary>
        public void Release() => _released.TrySetResult(true);

        async Task IBootstrappable.BootStrapAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attemptCount) >= 2)
            {
                // Hold from the second attempt onwards, before recording anything. The count is
                // incremented first, so a caller which sees it reach 2 knows the first attempt was
                // processed to completion and that no further mutation can occur until Release.
                await _released.Task.ConfigureAwait(false);
            }

            DeferredExceptions.Add(new Exception("Bootstrapping has failed."));
        }

        public bool IsBootstrapped => !DeferredExceptions.Any();
        public List<Exception> DeferredExceptions { get; } = new List<Exception>();
    }

    /// <summary>
    /// Clears the deferred failures before recording a new one, the way CouchbaseBucket does.
    /// </summary>
    public class ClearingBootstrappable : IBootstrappable
    {
        private readonly TaskCompletionSource<bool> _released =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _attemptCount;

        public int AttemptCount => Volatile.Read(ref _attemptCount);

        /// <summary>
        /// Unblocks the second and subsequent attempts.
        /// </summary>
        public void Release() => _released.TrySetResult(true);

        async Task IBootstrappable.BootStrapAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attemptCount) >= 2)
            {
                await _released.Task.ConfigureAwait(false);
            }

            DeferredExceptions.Clear();
            DeferredExceptions.Add(new Exception("Bootstrapping has failed."));
        }

        public bool IsBootstrapped => !DeferredExceptions.Any();
        public List<Exception> DeferredExceptions { get; } = new List<Exception>();
    }

    /// <summary>
    /// Throws from the bootstrap the way BucketBase does, rather than deferring.
    /// </summary>
    public class ThrowingBootstrappable : IBootstrappable
    {
        private readonly TaskCompletionSource<bool> _released =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _attemptCount;

        public int AttemptCount => Volatile.Read(ref _attemptCount);

        /// <summary>
        /// Unblocks the third and subsequent attempts.
        /// </summary>
        public void Release() => _released.TrySetResult(true);

        async Task IBootstrappable.BootStrapAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attemptCount) >= 3)
            {
                await _released.Task.ConfigureAwait(false);
            }

            throw new Exception("Bootstrapping has failed.");
        }

        public bool IsBootstrapped => !DeferredExceptions.Any();
        public List<Exception> DeferredExceptions { get; } = new List<Exception>();
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
