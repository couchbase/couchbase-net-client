using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.Retry;
using Xunit;

namespace Couchbase.UnitTests.Core.Retry
{
    public class TimespanExtensionTests
    {
        [Theory]
        [InlineData(1, 2, 0.5)]
        [InlineData(2, 2, 0.5)]
        [InlineData(2.5, 2.5, 0)]
        [InlineData(1, 0, 1)]
        [InlineData(1, 0.5, 1)]
        public void When_Lifetime_Is_Exceeded_Duration_Is_Capped(double durationSeconds, double elaspedSeconds, double expectedCappedDuration)
        {
            var lifetime = TimeSpan.FromSeconds(2.5);
            var duration = TimeSpan.FromSeconds(durationSeconds);
            var elasped = TimeSpan.FromSeconds(elaspedSeconds);

            var cappedDuration = lifetime.CappedDuration(duration, elasped);
            Assert.Equal(TimeSpan.FromSeconds(expectedCappedDuration), cappedDuration);
        }
    }
}
