using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    [Collection("NonParallel")]
    public class LightweightStopwatchTests
    {
        #region Elapsed

        [Fact]
        public void Elapsed_AfterStart_LowValue()
        {
            // Arrange

            var stopwatch = LightweightStopwatch.StartNew();

            // Act

            var result = stopwatch.Elapsed;

            // Assert

            Assert.True(result < TimeSpan.FromSeconds(1));
        }

        [Fact(Skip="Inconsistent behavior in underprovisioned Jenkins.")]
        public void Elapsed_AfterSleep_ApproximateValue()
        {
            // Arrange

            var oldSw = Stopwatch.StartNew();
            var stopwatch = LightweightStopwatch.StartNew();

            // Act

            SpinWait.SpinUntil(() => oldSw.ElapsedMilliseconds >= 1000);
            var result = stopwatch.ElapsedMilliseconds;
            var oldResult = oldSw.ElapsedMilliseconds;
            oldSw.Stop();

            // Assert
            Assert.InRange(result, oldResult - 250, oldResult + 250);
        }

        #endregion

        #region ElapsedMilliseconds

        [Fact]
        public void ElapsedMilliseconds_AfterStart_LowValue()
        {
            // Arrange

            var stopwatch = LightweightStopwatch.StartNew();

            // Act

            var result = stopwatch.ElapsedMilliseconds;

            // Assert

            Assert.True(result < 1000);
        }

        [Fact(Skip="Inconsistent behavior in underprovisioned Jenkins.")]
        public async Task ElapsedMilliseconds_AfterSleep_ApproximateValue()
        {
            // Arrange

            var stopwatch = LightweightStopwatch.StartNew();

            // Act

            await  Task.Delay(TimeSpan.FromMilliseconds(1000));
            var result = stopwatch.ElapsedMilliseconds;

            // Assert
            Assert.InRange(result, 750, 3000);
        }

        #endregion

        #region Restart

        [Fact]
        public async Task Restart_AfterStart_LowValue()
        {
            // Arrange

            var stopwatch = LightweightStopwatch.StartNew();
            await Task.Delay(1000);
            Assert.True(stopwatch.ElapsedMilliseconds > 500);

            // Act

            stopwatch.Restart();
            var result = stopwatch.ElapsedMilliseconds;

            // Assert

            Assert.True(result < 1000);
        }

        [Fact]
        public async Task Restart_AfterSleep_ApproximateValue()
        {
            // Arrange
            await Task.Yield();

            var stopwatch = LightweightStopwatch.StartNew();
            await Task.Delay(TimeSpan.FromMilliseconds(1000));
            Assert.True(stopwatch.ElapsedMilliseconds > 500);

            // Act

            stopwatch.Restart();
            await Task.Delay(TimeSpan.FromMilliseconds(1000));
            var result = stopwatch.ElapsedMilliseconds;

            // Assert

            Assert.True(result > 500);
        }

        #endregion
    }
}
