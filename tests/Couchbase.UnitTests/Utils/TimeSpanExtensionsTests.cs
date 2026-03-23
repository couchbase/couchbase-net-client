using System;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class TimeSpanExtensionsTests
    {
        [Fact]
        //Fixes the minimal amount of expiry supported by the server
        public void When_Less_Than_1000MS_Returns_OneSecond()
        {
            var lifespan = TimeSpan.FromMilliseconds(999).ToTtl();
            Assert.Equal(1u, lifespan);
        }

        [Fact]
        public void When_Equal_To_1000MS_Returns_OneSecond()
        {
            var lifespan = TimeSpan.FromMilliseconds(1000).ToTtl();
            Assert.Equal(1u, lifespan);
        }

        [Fact]
        public void When_Zero_Returns_Zero()
        {
            var lifespan = TimeSpan.Zero.ToTtl();
            Assert.Equal(0u, lifespan);
        }

        [Fact]
        public void When_Negative_Value_Returns_Zero()
        {
            var lifespan = TimeSpan.FromMilliseconds(-1).ToTtl();
            Assert.Equal(0u, lifespan);
        }

        #region ToEpochTtl

        [Fact]
        public void ToEpochTtl_Returns_AbsoluteExpiry_In_Future()
        {
            var duration = TimeSpan.FromMinutes(5);
            var before = DateTimeOffset.UtcNow;

            var result = duration.ToEpochTtl();

            var after = DateTimeOffset.UtcNow;
            Assert.True(result >= before + duration);
            Assert.True(result <= after + duration);
        }

        [Fact]
        public void ToEpochTtl_Zero_Returns_Now()
        {
            var before = DateTimeOffset.UtcNow;

            var result = TimeSpan.Zero.ToEpochTtl();

            var after = DateTimeOffset.UtcNow;
            Assert.True(result >= before);
            Assert.True(result <= after);
        }

        [Fact]
        public void ToEpochTtl_LargeDuration_Returns_FarFuture()
        {
            var duration = TimeSpan.FromDays(365);
            var before = DateTimeOffset.UtcNow;

            var result = duration.ToEpochTtl();

            Assert.True(result > before.AddDays(364));
        }

        #endregion

        #region RemainingTtl

        [Fact]
        public void RemainingTtl_FutureExpiry_Returns_RemainingTime()
        {
            var absoluteExpiry = DateTimeOffset.UtcNow.AddMinutes(10);

            var result = absoluteExpiry.RemainingTtl();

            Assert.True(result > TimeSpan.FromMinutes(9));
            Assert.True(result <= TimeSpan.FromMinutes(10));
        }

        [Fact]
        public void RemainingTtl_PastExpiry_Returns_OneSecond()
        {
            var absoluteExpiry = DateTimeOffset.UtcNow.AddMinutes(-5);

            var result = absoluteExpiry.RemainingTtl();

            Assert.Equal(TimeSpan.FromSeconds(1), result);
        }

        [Fact]
        public void RemainingTtl_ExpiryNow_Returns_OneSecond()
        {
            var absoluteExpiry = DateTimeOffset.UtcNow;

            var result = absoluteExpiry.RemainingTtl();

            Assert.Equal(TimeSpan.FromSeconds(1), result);
        }

        [Fact]
        public void RemainingTtl_ExpiryLessThanOneSecond_Returns_OneSecond()
        {
            var absoluteExpiry = DateTimeOffset.UtcNow.AddMilliseconds(500);

            var result = absoluteExpiry.RemainingTtl();

            Assert.Equal(TimeSpan.FromSeconds(1), result);
        }

        [Fact]
        public void RemainingTtl_ExpiryJustOverOneSecond_Returns_RemainingTime()
        {
            var absoluteExpiry = DateTimeOffset.UtcNow.AddSeconds(2);

            var result = absoluteExpiry.RemainingTtl();

            Assert.True(result > TimeSpan.FromSeconds(1));
            Assert.True(result <= TimeSpan.FromSeconds(2));
        }

        #endregion

        #region Roundtrip ToEpochTtl/RemainingTtl

        [Fact]
        public void ToEpochTtl_RemainingTtl_Roundtrip_PreservesApproximateDuration()
        {
            var originalDuration = TimeSpan.FromMinutes(30);

            var absoluteExpiry = originalDuration.ToEpochTtl();
            var remainingTtl = absoluteExpiry.RemainingTtl();

            // Should be very close to the original, within a small tolerance for execution time
            Assert.True(remainingTtl > originalDuration - TimeSpan.FromSeconds(1));
            Assert.True(remainingTtl <= originalDuration);
        }

        #endregion
    }
}
