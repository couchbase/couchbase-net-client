using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    // Tests verify the wrapper's contract (non-negative, consistent, monotonic, restart resets)
    // without Task.Delay or fixed timing bounds. SpinWait is used only to advance the clock
    // by at least one tick, which is deterministic regardless of machine load.
    public class LightweightStopwatchTests
    {
        #region StartNew / Elapsed

        [Fact]
        public void StartNew_Elapsed_IsNonNegative()
        {
            var sw = LightweightStopwatch.StartNew();

            Assert.True(sw.Elapsed >= TimeSpan.Zero);
        }

        [Fact]
        public void StartNew_ElapsedMilliseconds_IsNonNegative()
        {
            var sw = LightweightStopwatch.StartNew();

            Assert.True(sw.ElapsedMilliseconds >= 0);
        }

        [Fact]
        public void StartNew_ElapsedMilliseconds_IsLow()
        {
            var sw = LightweightStopwatch.StartNew();

            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"ElapsedMilliseconds should be near zero immediately after StartNew, was {sw.ElapsedMilliseconds}");
        }

        [Fact]
        public void Elapsed_And_ElapsedMilliseconds_AreConsistent()
        {
            var sw = LightweightStopwatch.StartNew();
            SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > 0);

            var elapsedMs = sw.ElapsedMilliseconds;
            var elapsedTimeSpanMs = (long)sw.Elapsed.TotalMilliseconds;

            // They should agree within a small tolerance (one could tick between reads)
            Assert.True(Math.Abs(elapsedTimeSpanMs - elapsedMs) < 50,
                $"Elapsed ({elapsedTimeSpanMs}ms) and ElapsedMilliseconds ({elapsedMs}ms) should be consistent");
        }

        [Fact]
        public void Elapsed_IsMonotonicallyIncreasing()
        {
            var sw = LightweightStopwatch.StartNew();
            var first = sw.ElapsedMilliseconds;
            SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > first);
            var second = sw.ElapsedMilliseconds;

            Assert.True(second > first,
                $"Expected second reading ({second}) > first reading ({first})");
        }

        #endregion

        #region Restart

        [Fact]
        public void Restart_ResetsElapsed()
        {
            var sw = LightweightStopwatch.StartNew();

            // Ensure elapsed advances past zero
            SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > 0);
            var before = sw.ElapsedMilliseconds;
            Assert.True(before > 0);

            sw.Restart();
            var after = sw.ElapsedMilliseconds;

            Assert.True(after < before,
                $"After Restart, ElapsedMilliseconds ({after}) should be less than before ({before})");
        }

        [Fact]
        public void Restart_ElapsedContinuesToAdvance()
        {
            var sw = LightweightStopwatch.StartNew();
            SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > 0);

            sw.Restart();

            // After restart, it should still be advancing
            var afterRestart = sw.ElapsedMilliseconds;
            SpinWait.SpinUntil(() => sw.ElapsedMilliseconds > afterRestart);

            Assert.True(sw.ElapsedMilliseconds > afterRestart,
                "Stopwatch should continue to advance after Restart");
        }

        #endregion
    }
}
