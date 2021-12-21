using System;
using System.Threading.Tasks;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
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

        [Fact]
        public async Task Elapsed_AfterSleep_ApproximateValue()
        {
            // Arrange

            var stopwatch = LightweightStopwatch.StartNew();

            // Act

            await Task.Delay(1000);
            var result = stopwatch.Elapsed;

            // Assert

            Assert.True(Math.Abs((result - TimeSpan.FromSeconds(1)).TotalMilliseconds) < 250);
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

        [Fact]
        public async Task ElapsedMilliseconds_AfterSleep_ApproximateValue()
        {
            // Arrange

            var stopwatch = LightweightStopwatch.StartNew();

            // Act

            await Task.Delay(1000);
            var result = stopwatch.ElapsedMilliseconds;

            // Assert

            Assert.True(Math.Abs(result - 1000) < 250);
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

            var stopwatch = LightweightStopwatch.StartNew();
            await Task.Delay(1000);
            Assert.True(stopwatch.ElapsedMilliseconds > 500);

            // Act

            stopwatch.Restart();
            await Task.Delay(1000);
            var result = stopwatch.ElapsedMilliseconds;

            // Assert

            Assert.True(Math.Abs(result - 1000) < 250);
        }

        #endregion
    }
}
