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
    /// <summary>
    /// The bootstrapper polls in a fire-and-forget loop, so these tests run it on a
    /// <see cref="SteppableClock"/> and step it one lap at a time rather than waiting for a real sleep
    /// to elapse. Reaching the sleep means the lap has been processed to completion, so each assertion
    /// runs against a loop which can no longer mutate what is being asserted, and a loop which stops
    /// making progress hangs — and is reported as such — rather than failing a wall-clock deadline on
    /// a busy CI runner (NCBC-4293).
    /// </summary>
    public class BootstrapperTests
    {
        /// <summary>
        /// Long enough that a real sleep of this length would obviously hang the test rather than
        /// quietly pass, and no cost at all on a fake clock.
        /// </summary>
        private static readonly TimeSpan SleepDuration = TimeSpan.FromSeconds(30);

        private static ILogger<Bootstrapper> Logger => new Mock<ILogger<Bootstrapper>>().Object;

        private static Bootstrapper CreateBootstrapper(SteppableClock clock) =>
            new(Logger, clock)
            {
                SleepDuration = SleepDuration
            };

        [Fact]
        public async Task When_Cannot_Bootstrap_Repeat()
        {
            var clock = new SteppableClock();
            var mockSubject = new Mock<IBootstrappable>();
            mockSubject.Setup(x => x.BootStrapAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            mockSubject.Setup(x => x.DeferredExceptions).Returns(new List<Exception>());

            using var bootStrapper = CreateBootstrapper(clock);
            bootStrapper.Start(mockSubject.Object);

            await clock.WaitForSleepAsync(SleepDuration);
            clock.Advance(SleepDuration);
            await clock.WaitForSleepAsync(SleepDuration);

            // The subject never reports itself bootstrapped, so each lap must attempt it again.
            mockSubject.Verify(x => x.BootStrapAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task When_Success_Bootstrap_Do_Not_Repeat()
        {
            var clock = new SteppableClock();
            var mockSubject = new Mock<IBootstrappable>();
            mockSubject.Setup(x => x.IsBootstrapped).Returns(true);

            using var bootStrapper = CreateBootstrapper(clock);
            bootStrapper.Start(mockSubject.Object);

            // Reaching the sleep means a whole lap ran and declined to bootstrap; a second lap proves
            // it stays declined. Neither needs the test to wait and see.
            await clock.WaitForSleepAsync(SleepDuration);
            clock.Advance(SleepDuration);
            await clock.WaitForSleepAsync(SleepDuration);

            mockSubject.Verify(x => x.BootStrapAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_Success_IsBootstrapped_Is_True(bool failed)
        {
            var clock = new SteppableClock();
            var subject = new FakeBootstrappable(failed);

            // Start un-bootstrapped either way, so a subject which reports success reports it because
            // the bootstrapper made it so, not because that was its state to begin with.
            subject.DeferredExceptions.Add(new Exception("Earlier failure."));

            using var bootStrapper = CreateBootstrapper(clock);
            bootStrapper.Start(subject);

            await clock.WaitForSleepAsync(SleepDuration);

            Assert.Equal(1, subject.AttemptCount);
            if (failed)
            {
                Assert.False(subject.IsBootstrapped);
                Assert.NotEmpty(subject.DeferredExceptions);
            }
            else
            {
                Assert.True(subject.IsBootstrapped);
                Assert.Empty(subject.DeferredExceptions);
            }
        }

        [Fact]
        public async Task When_Bootstrap_Defers_Failure_Without_Throwing_Failure_Is_Not_Cleared()
        {
            // Cluster.BootStrapAsync records a bootstrap failure in DeferredExceptions and returns
            // normally rather than throwing, so a normal return must not be reported as success.
            var clock = new SteppableClock();
            var subject = new DeferringBootstrappable();

            // Start un-bootstrapped, so the bootstrapper actually makes an attempt.
            subject.DeferredExceptions.Add(new Exception("Earlier failure."));

            using var bootStrapper = CreateBootstrapper(clock);
            bootStrapper.Start(subject);

            await clock.WaitForSleepAsync(SleepDuration);

            // This attempt's deferred failure survived the attempt, so the failure was not mistaken
            // for a success — while the stale one it replaced was still discarded.
            Assert.False(subject.IsBootstrapped);
            Assert.Single(subject.DeferredExceptions);

            clock.Advance(SleepDuration);
            await clock.WaitForSleepAsync(SleepDuration);

            // Retrying at all confirms the loop did not stop, and repeated attempts do not accumulate.
            Assert.Equal(2, subject.AttemptCount);
            Assert.Single(subject.DeferredExceptions);
        }

        [Fact]
        public async Task When_Subject_Clears_Its_Own_Failures_This_Attempts_Failure_Survives()
        {
            // CouchbaseBucket clears DeferredExceptions itself before recording a new failure, so the
            // stale entries cannot be discarded by position — that would remove this attempt's failure
            // instead, report success and stop the retry loop.
            var clock = new SteppableClock();
            var subject = new ClearingBootstrappable();

            // Start un-bootstrapped, so the bootstrapper actually makes an attempt.
            subject.DeferredExceptions.Add(new Exception("Earlier failure."));

            using var bootStrapper = CreateBootstrapper(clock);
            bootStrapper.Start(subject);

            await clock.WaitForSleepAsync(SleepDuration);

            Assert.False(subject.IsBootstrapped);
            Assert.Single(subject.DeferredExceptions);

            clock.Advance(SleepDuration);
            await clock.WaitForSleepAsync(SleepDuration);

            Assert.Equal(2, subject.AttemptCount);
            Assert.Single(subject.DeferredExceptions);
        }

        [Fact]
        public async Task When_Bootstrap_Throws_Failures_Do_Not_Accumulate()
        {
            // BucketBase.BootStrapAsync throws rather than defers, and the bootstrapper records the
            // exception on every poll. Stale failures must still be discarded or the list grows
            // without bound for as long as the subject stays broken.
            var clock = new SteppableClock();
            var subject = new ThrowingBootstrappable();

            // Start un-bootstrapped, so the bootstrapper actually makes an attempt.
            subject.DeferredExceptions.Add(new Exception("Earlier failure."));

            using var bootStrapper = CreateBootstrapper(clock);
            bootStrapper.Start(subject);

            await clock.WaitForSleepAsync(SleepDuration);

            Assert.False(subject.IsBootstrapped);
            Assert.Single(subject.DeferredExceptions);

            clock.Advance(SleepDuration);
            await clock.WaitForSleepAsync(SleepDuration);

            // Only the most recent attempt's failure is retained, however many attempts have run.
            Assert.Equal(2, subject.AttemptCount);
            Assert.Single(subject.DeferredExceptions);
        }

        [Fact]
        public async Task Bootstrap_Is_Not_Retried_Until_The_Sleep_Duration_Has_Elapsed()
        {
            var clock = new SteppableClock();
            var subject = new DeferringBootstrappable();
            subject.DeferredExceptions.Add(new Exception("Earlier failure."));

            using var bootStrapper = CreateBootstrapper(clock);
            bootStrapper.Start(subject);

            await clock.WaitForSleepAsync(SleepDuration);

            // Just short of the configured duration is not enough: the loop is waiting on the clock it
            // was given, for as long as it was told to.
            clock.Advance(SleepDuration - TimeSpan.FromTicks(1));
            Assert.Equal(1, subject.AttemptCount);

            clock.Advance(TimeSpan.FromTicks(1));
            await clock.WaitForSleepAsync(SleepDuration);

            Assert.Equal(2, subject.AttemptCount);
        }

        /// <summary>
        /// Common state for the subjects below, which differ only in how an attempt records its
        /// outcome — the behaviour the bootstrapper has to cope with.
        /// </summary>
        private abstract class BootstrappableBase : IBootstrappable
        {
            private int _attemptCount;

            public int AttemptCount => Volatile.Read(ref _attemptCount);

            Task IBootstrappable.BootStrapAsync(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _attemptCount);
                Attempt();
                return Task.CompletedTask;
            }

            /// <summary>
            /// Records the outcome of one bootstrap attempt, the way the real subject would.
            /// </summary>
            protected abstract void Attempt();

            public bool IsBootstrapped => !DeferredExceptions.Any();
            public List<Exception> DeferredExceptions { get; } = new();
        }

        /// <summary>
        /// Records a bootstrap failure the way <see cref="Cluster"/> does — deferred, without throwing.
        /// </summary>
        private sealed class DeferringBootstrappable : BootstrappableBase
        {
            protected override void Attempt() =>
                DeferredExceptions.Add(new Exception("Bootstrapping has failed."));
        }

        /// <summary>
        /// Clears the deferred failures before recording a new one, the way CouchbaseBucket does.
        /// </summary>
        private sealed class ClearingBootstrappable : BootstrappableBase
        {
            protected override void Attempt()
            {
                DeferredExceptions.Clear();
                DeferredExceptions.Add(new Exception("Bootstrapping has failed."));
            }
        }

        /// <summary>
        /// Throws from the bootstrap the way BucketBase does, rather than deferring.
        /// </summary>
        private sealed class ThrowingBootstrappable : BootstrappableBase
        {
            protected override void Attempt() => throw new Exception("Bootstrapping has failed.");
        }

        /// <summary>
        /// Fails by throwing, or succeeds by clearing the failures which held it back — as a subject
        /// which has just bootstrapped does.
        /// </summary>
        private sealed class FakeBootstrappable : BootstrappableBase
        {
            private readonly bool _hasFailed;

            public FakeBootstrappable(bool hasFailed)
            {
                _hasFailed = hasFailed;
            }

            protected override void Attempt()
            {
                if (_hasFailed)
                {
                    throw new Exception("Bootstrapping has failed.");
                }

                DeferredExceptions.Clear();
            }
        }
    }
}
